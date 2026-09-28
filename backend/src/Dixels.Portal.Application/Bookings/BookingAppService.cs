using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Bookings;

/* Each action needs its Bookings permission. On top of that, only the owner (or someone with Bookings.ManageAll)
 * may change a given booking; that depends on the booking, so it is checked in code (EnsureCanActAsync). */
[Authorize]
public class BookingAppService : PortalAppService, IBookingAppService
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;
    private readonly BookingManager _manager;
    private readonly MaintenanceScopeResolver _scopes;

    public BookingAppService(IRepository<Booking, Guid> bookings, IRepository<Space, Guid> spaces,
        IIdentityUserRepository users, BookingManager manager, MaintenanceScopeResolver scopes)
    {
        _bookings = bookings;
        _spaces = spaces;
        _users = users;
        _manager = manager;
        _scopes = scopes;
    }

    /* Admins (Bookings.ManageAll) list anyone's bookings; everyone else only ever gets their own,
     * whatever owner they ask for. Other people's time comes from GetBusyListAsync instead. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input)
    {
        var ownerId = await SeesAllBookingsAsync() ? input.OwnerUserId : CurrentUser.GetId();
        var query = await _bookings.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(b => b.Status == BookingStatus.Confirmed);
        if (input.SpaceId.HasValue) query = query.Where(b => b.SpaceId == input.SpaceId.Value);
        if (ownerId.HasValue) query = query.Where(b => b.OwnerUserId == ownerId.Value);
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc));
        return new ListResultDto<BookingDto>(await MapListAsync(list));
    }

    /* Someone else's booking is "not found" for a non-admin, exactly like a booking that doesn't exist. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<BookingDto> GetAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        if (b.OwnerUserId != CurrentUser.Id && !await SeesAllBookingsAsync())
            throw new BusinessException(PortalDomainErrorCodes.BookingNotFound, "No booking with that ID.");
        return await MapAsync(b);
    }

    /* Only when and where: enough to show a grey "Busy" block and to know a time is taken. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<ListResultDto<BusyWindowDto>> GetBusyListAsync(BusyListFilterDto input)
    {
        var me = CurrentUser.Id;
        var query = (await _bookings.GetQueryableAsync())
            .Where(b => b.Status == BookingStatus.Confirmed && b.OwnerUserId != me);
        if (input.SpaceId.HasValue) query = query.Where(b => b.SpaceId == input.SpaceId.Value);
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var windows = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc)
            .Select(b => new BusyWindowDto { SpaceId = b.SpaceId, StartUtc = b.StartUtc, EndUtc = b.EndUtc }));
        return new ListResultDto<BusyWindowDto>(windows);
    }

    private Task<bool> SeesAllBookingsAsync() => AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.ManageAll);

    [Authorize(PortalPermissions.Bookings.Create)]
    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey;

        /* A retried request (same key) gets the booking the first attempt made, not a second one. */
        if (key != null)
        {
            var existing = await _bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == key);
            if (existing != null) return await MapAsync(existing);
        }

        return await MapAsync(await CreateOneAsync(input.SpaceId, input.StartUtc, input.EndUtc, null, key));
    }

    /* The client expands the recurrence rule; each surviving occurrence is created under one series.
     * An occurrence that breaks a rule is skipped and reported; the rest are still booked. */
    [Authorize(PortalPermissions.Bookings.Create)]
    public async Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input)
    {
        var seriesId = input.Occurrences.Count > 1 ? GuidGenerator.Create() : (Guid?)null;
        var created = new List<Booking>();
        var skipped = new List<BookingWindowFailureDto>();
        foreach (var o in input.Occurrences.OrderBy(o => o.StartUtc))
        {
            try
            {
                created.Add(await CreateOneAsync(input.SpaceId, o.StartUtc, o.EndUtc, seriesId, null));
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
            Created = await MapListAsync(created),
            Skipped = skipped,
        };
    }

    [Authorize(PortalPermissions.Bookings.Edit)]
    public async Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, "You can only change your own bookings.");
        var state = b.GetLifecycle(Clock.Now);
        if (state is TimeWindowState.Ended or TimeWindowState.Cancelled)
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, $"A {state.ToApiValue()} booking cannot be changed.");
        if (state == TimeWindowState.InProgress)
            throw new BusinessException(PortalDomainErrorCodes.BookingInProgress,
                "A booking that has started can only be cancelled or ended early.");
        /* Optimistic concurrency: refuse if someone changed the booking after the client loaded it. */
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
        return await MapAsync(b);
    }

    [Authorize(PortalPermissions.Bookings.Delete)]
    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await CancelOneAsync(b);
        return await MapAsync(b);
    }

    /* Cancels this booking and every later one in its series; ended occurrences are left alone. */
    [Authorize(PortalPermissions.Bookings.Delete)]
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
        foreach (var b in targets.Where(b => b.GetLifecycle(Clock.Now) != TimeWindowState.Ended))
        {
            await CancelOneAsync(b);
            count++;
        }
        return new CancelSeriesResultDto { SeriesId = seriesId, CancelledCount = count };
    }

    /* Frees the space now: the booking's end moves to the current time. */
    [Authorize(PortalPermissions.Bookings.Edit)]
    public async Task<BookingDto> EndEarlyAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, "You can only end your own bookings.");
        if (b.GetLifecycle(Clock.Now) != TimeWindowState.InProgress)
            throw new BusinessException(PortalDomainErrorCodes.BookingNotInProgress,
                "Only a booking that has already started can be ended early.");
        b.EndUtc = Clock.Now;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        return await MapAsync(b);
    }

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public async Task<int> GetUpcomingCountAsync(EstateScopeDto input)
        => await AsyncExecuter.CountAsync(await UpcomingInScopeAsync(input));

    /* Admin: cancel every upcoming booking in a scope, e.g. after making it not bookable. */
    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public async Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input)
    {
        var upcoming = await AsyncExecuter.ToListAsync(await UpcomingInScopeAsync(input));
        foreach (var b in upcoming) b.Cancel();
        await _bookings.UpdateManyAsync(upcoming, autoSave: true);
        return new CancelUpcomingResultDto { CancelledCount = upcoming.Count };
    }

    private async Task<Booking> GetBookingAsync(Guid id)
        => await _bookings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.BookingNotFound, "No booking with that ID.");

    /* Owners act on their own bookings; admins (Bookings.ManageAll) on anyone's. */
    private async Task EnsureCanActAsync(Booking b, string message)
    {
        if (b.OwnerUserId != CurrentUser.Id && !await AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.ManageAll))
            throw new BusinessException(PortalDomainErrorCodes.AccessForbidden, message);
    }

    private async Task<Booking> CreateOneAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? seriesId, string? idempotencyKey)
    {
        var booking = await _manager.CreateAsync(spaceId, CurrentUser.GetId(), startUtc.AsUtc(), endUtc.AsUtc(),
            seriesId: seriesId, idempotencyKey: idempotencyKey);
        await _bookings.InsertAsync(booking, autoSave: true);
        return booking;
    }

    private async Task CancelOneAsync(Booking b)
    {
        await EnsureCanActAsync(b, "You can only cancel your own bookings.");
        var state = b.GetLifecycle(Clock.Now);
        if (state == TimeWindowState.Ended)
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, "An ended booking cannot be cancelled.");
        if (state == TimeWindowState.Cancelled) return;
        b.Cancel();
        await _bookings.UpdateAsync(b, autoSave: true);
    }

    /* "Upcoming" = confirmed and not started yet; a meeting already in progress is never cut off. */
    private async Task<IQueryable<Booking>> UpcomingInScopeAsync(EstateScopeDto scope)
    {
        var spaceIds = await _scopes.FindSpaceIdsAsync(scope.ScopeType, scope.ScopeId);
        var now = Clock.Now;
        return (await _bookings.GetQueryableAsync())
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartUtc > now);
    }

    private async Task<BookingDto> MapAsync(Booking booking) => (await MapListAsync(new List<Booking> { booking }))[0];

    /* Shown instead of the owner's name on other people's bookings to anyone who is not an admin. */
    public const string HiddenOwnerName = "Booked";

    /* ObjectMapper copies the booking; the space and owner names come from one query each for the whole list.
     * Only admins (Bookings.ManageAll) see who booked what; everyone else sees their own name and
     * "Booked" on the rest, so another person's name never leaves the server. */
    private async Task<List<BookingDto>> MapListAsync(List<Booking> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var userIds = list.Select(b => b.OwnerUserId).Distinct().ToList();
        var users = (await _users.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id, u => u.GetDisplayName());
        var now = Clock.Now;
        var seesNames = await SeesAllBookingsAsync();
        return list.Select(b =>
        {
            var dto = ObjectMapper.Map<Booking, BookingDto>(b);
            dto.SpaceName = spaces.GetValueOrDefault(b.SpaceId, "Unknown space");
            dto.OwnerName = seesNames || b.OwnerUserId == CurrentUser.Id
                ? users.GetValueOrDefault(b.OwnerUserId, "a former user")
                : HiddenOwnerName;
            dto.Lifecycle = b.GetLifecycle(now).ToApiValue();
            return dto;
        }).ToList();
    }
}
