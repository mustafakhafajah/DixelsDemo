using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Dixels.Portal.Bookings;

[Authorize]
public class BookingAppService : EstateAppServiceBase, IBookingAppService
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingManager _manager;
    private readonly BookingDtoMapper _mapper;

    public BookingAppService(IRepository<Booking, Guid> bookings, BookingManager manager, BookingDtoMapper mapper)
    {
        _bookings = bookings;
        _manager = manager;
        _mapper = mapper;
    }

    public async Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input)
    {
        var query = await _bookings.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(b => b.Status == BookingStatus.Confirmed);
        if (input.SpaceId.HasValue) query = query.Where(b => b.SpaceId == input.SpaceId.Value);
        if (input.OwnerUserId.HasValue) query = query.Where(b => b.OwnerUserId == input.OwnerUserId.Value);
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc));
        return new ListResultDto<BookingDto>(await _mapper.MapListAsync(list));
    }

    public async Task<BookingDto> GetAsync(Guid id) => await _mapper.MapAsync(await GetBookingAsync(id));

    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            var existing = await _bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == input.IdempotencyKey);
            if (existing != null) return await _mapper.MapAsync(existing);
        }

        var booking = await CreateOneAsync(input.SpaceId, input.StartUtc, input.EndUtc, input.Parking, null,
            string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey);
        return await _mapper.MapAsync(booking);
    }

    /* The client expands the recurrence rule; each surviving occurrence is created under one series. */
    public async Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input)
    {
        var seriesId = input.Occurrences.Count > 1 ? GuidGenerator.Create() : (Guid?)null;
        var created = new List<Booking>();
        var skipped = new List<BookingWindowFailureDto>();
        foreach (var o in input.Occurrences.OrderBy(o => o.StartUtc))
        {
            try
            {
                created.Add(await CreateOneAsync(input.SpaceId, o.StartUtc, o.EndUtc, input.Parking, seriesId, null));
            }
            catch (BusinessException ex)
            {
                skipped.Add(new BookingWindowFailureDto
                {
                    StartUtc = o.StartUtc, EndUtc = o.EndUtc,
                    ErrorCode = ex.Code ?? "unknown", ErrorMessage = ex.Message,
                });
            }
        }

        return new CreateBookingSeriesResultDto
        {
            SeriesId = created.Count > 0 ? seriesId : null,
            Created = await _mapper.MapListAsync(created),
            Skipped = skipped,
        };
    }

    public async Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, "You can only change your own bookings.");
        var state = b.GetLifecycle(Clock.Now);
        if (state is "ended" or "cancelled")
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, $"A {state} booking cannot be changed.");
        if (state == "in_progress")
            throw new BusinessException(PortalDomainErrorCodes.BookingInProgress,
                "A booking that has started can only be cancelled or ended early.");
        if (input.ExpectedVersion.HasValue && input.ExpectedVersion.Value != b.Version)
            throw new BusinessException(PortalDomainErrorCodes.VersionMismatch,
                    "This booking changed since you loaded it. Reload and try again.")
                .WithData("expected", input.ExpectedVersion.Value)
                .WithData("current", b.Version);

        var start = input.StartUtc.AsUtc();
        var end = input.EndUtc.AsUtc();
        var ctx = await _manager.GetSpaceContextAsync(b.SpaceId);
        _manager.ValidateWindow(ctx, start, end);
        await _manager.EnsureNoMaintenanceAsync(ctx, start, end);
        await _manager.EnsureNoConflictAsync(b.SpaceId, start, end, b.Id);
        await _manager.EnsureNoSelfOverlapAsync(b.OwnerUserId, b.SpaceId, start, end, b.Id);

        b.StartUtc = start;
        b.EndUtc = end;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        return await _mapper.MapAsync(b);
    }

    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await CancelOneAsync(b);
        return await _mapper.MapAsync(b);
    }

    public async Task<CancelSeriesResultDto> CancelSeriesFromAsync(Guid id)
    {
        var anchor = await GetBookingAsync(id);
        await EnsureCanActAsync(anchor, "You can only cancel your own bookings.");
        if (!anchor.SeriesId.HasValue)
        {
            await CancelOneAsync(anchor);
            return new CancelSeriesResultDto { CancelledCount = 1 };
        }

        var seriesId = anchor.SeriesId.Value;
        var from = anchor.StartUtc;
        var targets = await _bookings.GetListAsync(b =>
            b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartUtc >= from);
        var count = 0;
        foreach (var b in targets.Where(b => b.GetLifecycle(Clock.Now) != "ended"))
        {
            await CancelOneAsync(b);
            count++;
        }
        return new CancelSeriesResultDto { SeriesId = seriesId, CancelledCount = count };
    }

    public async Task<BookingDto> EndEarlyAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, "You can only end your own bookings.");
        if (b.GetLifecycle(Clock.Now) != "in_progress")
            throw new BusinessException(PortalDomainErrorCodes.BookingNotInProgress,
                "Only a booking that has already started can be ended early.");
        b.EndUtc = Clock.Now;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        return await _mapper.MapAsync(b);
    }

    private async Task<Booking> CreateOneAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, bool parking,
        Guid? seriesId, string? idempotencyKey)
    {
        /* Parking is an admin-only extra: who is asking is an application concern, so it's decided here. */
        var isAdmin = await IsAdminAsync();
        var booking = await _manager.CreateAsync(spaceId, CurrentUser.GetId(), startUtc.AsUtc(), endUtc.AsUtc(),
            parking: isAdmin && parking, seriesId: seriesId, idempotencyKey: idempotencyKey);
        await _bookings.InsertAsync(booking, autoSave: true);
        return booking;
    }

    private async Task CancelOneAsync(Booking b)
    {
        await EnsureCanActAsync(b, "You can only cancel your own bookings.");
        var state = b.GetLifecycle(Clock.Now);
        if (state == "ended")
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, "An ended booking cannot be cancelled.");
        if (state == "cancelled") return;
        b.Status = BookingStatus.Cancelled;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
    }

    private async Task EnsureCanActAsync(Booking b, string message)
    {
        if (b.OwnerUserId != CurrentUser.Id && !await IsAdminAsync())
            throw new BusinessException(PortalDomainErrorCodes.AccessForbidden, message);
    }

    private async Task<Booking> GetBookingAsync(Guid id)
        => await _bookings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.BookingNotFound, "No booking with that ID.");
}
