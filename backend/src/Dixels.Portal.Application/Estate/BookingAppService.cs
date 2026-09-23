using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Estate;

[Authorize]
public class BookingAppService : EstateAppServiceBase, IBookingAppService
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;
    private readonly IRepository<Team, Guid> _teams;
    private readonly BookingManager _manager;
    private readonly ActivityLogAppender _log;

    public BookingAppService(IRepository<Booking, Guid> bookings, IRepository<Space, Guid> spaces,
        IIdentityUserRepository users, IRepository<Team, Guid> teams, BookingManager manager, ActivityLogAppender log)
    {
        _bookings = bookings;
        _spaces = spaces;
        _users = users;
        _teams = teams;
        _manager = manager;
        _log = log;
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
        return new ListResultDto<BookingDto>(await MapManyAsync(list));
    }

    public async Task<BookingDto> GetAsync(Guid id) => (await MapManyAsync(new() { await GetBookingAsync(id) }))[0];

    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            var existing = await _bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == input.IdempotencyKey);
            if (existing != null) return (await MapManyAsync(new() { existing }))[0];
        }

        var booking = await CreateOneAsync(input.SpaceId, input.StartUtc, input.EndUtc, input.Parking, null,
            string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey);
        return (await MapManyAsync(new() { booking }))[0];
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
            Created = await MapManyAsync(created),
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

        var start = Utc(input.StartUtc);
        var end = Utc(input.EndUtc);
        var ctx = await _manager.GetSpaceContextAsync(b.SpaceId);
        _manager.ValidateWindow(ctx, start, end);
        await _manager.EnsureNoMaintenanceAsync(ctx, start, end);
        await _manager.EnsureNoConflictAsync(b.SpaceId, start, end, b.Id);
        await _manager.EnsureNoSelfOverlapAsync(b.OwnerUserId, b.SpaceId, start, end, b.Id);

        var before = $"{BookingManager.Stamp(b.StartUtc)} → {BookingManager.Hm(b.EndUtc)}";
        b.StartUtc = start;
        b.EndUtc = end;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        var onBehalf = b.OwnerUserId != CurrentUser.Id ? $" · on behalf of {await OwnerNameAsync(b.OwnerUserId)}" : "";
        await _log.LogAsync(ActivityActions.BookingRescheduled, ActivityEntityTypes.Booking, b.Id,
            $"{before}  ⇒  {BookingManager.Stamp(start)} → {BookingManager.Hm(end)}{onBehalf}");
        return (await MapManyAsync(new() { b }))[0];
    }

    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await CancelOneAsync(b);
        return (await MapManyAsync(new() { b }))[0];
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
        var space = await _spaces.GetAsync(b.SpaceId);
        await _log.LogAsync(ActivityActions.BookingEndedEarly, ActivityEntityTypes.Booking, b.Id,
            $"{space.Name} · ended at {BookingManager.Hm(b.EndUtc)}");
        return (await MapManyAsync(new() { b }))[0];
    }

    private async Task<Booking> CreateOneAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, bool parking,
        Guid? seriesId, string? idempotencyKey)
    {
        var userId = CurrentUser.GetId();
        var start = Utc(startUtc);
        var end = Utc(endUtc);
        var isAdmin = await IsAdminAsync();
        var ctx = await _manager.GetSpaceContextAsync(spaceId);
        var teamId = await _manager.GetUserTeamIdAsync(userId);
        await _manager.ValidateNewBookingAsync(ctx, userId, teamId, isAdmin, start, end);

        var booking = new Booking(GuidGenerator.Create(), spaceId, userId, start, end)
        {
            SeriesId = seriesId,
            Parking = isAdmin && parking,
            IdempotencyKey = idempotencyKey,
        };
        await _bookings.InsertAsync(booking, autoSave: true);
        await _log.LogAsync(ActivityActions.BookingCreated, ActivityEntityTypes.Booking, booking.Id,
            $"{ctx.Space.Name} · {BookingManager.Stamp(start)} → {BookingManager.Hm(end)}");
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
        var space = await _spaces.GetAsync(b.SpaceId);
        var byAdmin = b.OwnerUserId != CurrentUser.Id ? " · cancelled by administrator" : "";
        await _log.LogAsync(ActivityActions.BookingCancelled, ActivityEntityTypes.Booking, b.Id,
            $"{space.Name} · {BookingManager.Stamp(b.StartUtc)} → {BookingManager.Hm(b.EndUtc)}{byAdmin}");
    }

    private async Task EnsureCanActAsync(Booking b, string message)
    {
        if (b.OwnerUserId != CurrentUser.Id && !await IsAdminAsync())
            throw new BusinessException(PortalDomainErrorCodes.AccessForbidden, message);
    }

    private async Task<Booking> GetBookingAsync(Guid id)
        => await _bookings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.BookingNotFound, "No booking with that ID.");

    private async Task<string> OwnerNameAsync(Guid userId)
    {
        var u = await _users.FindAsync(userId, includeDetails: false);
        return u == null ? "a former user" : DisplayName(u);
    }

    public static string DisplayName(IdentityUser u)
    {
        var full = $"{u.Name} {u.Surname}".Trim();
        return full.Length > 0 ? full : u.UserName;
    }

    private static DateTime Utc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };

    private async Task<List<BookingDto>> MapManyAsync(List<Booking> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var userIds = list.Select(b => b.OwnerUserId).Distinct().ToList();
        var userEntities = await _users.GetListByIdsAsync(userIds);
        var users = userEntities.ToDictionary(u => u.Id, DisplayName);
        var teamNames = (await _teams.GetListAsync()).ToDictionary(t => t.Id, t => t.Name);
        var userTeams = userEntities.ToDictionary(u => u.Id, u =>
        {
            var teamId = u.GetProperty<Guid?>(BookingManager.TeamIdProperty);
            return teamId.HasValue ? teamNames.GetValueOrDefault(teamId.Value) : null;
        });
        var now = Clock.Now;
        return list.Select(b => new BookingDto
        {
            Id = b.Id,
            SpaceId = b.SpaceId,
            SpaceName = spaces.GetValueOrDefault(b.SpaceId, "Unknown space"),
            OwnerUserId = b.OwnerUserId,
            OwnerName = users.GetValueOrDefault(b.OwnerUserId, "a former user"),
            OwnerTeamName = userTeams.GetValueOrDefault(b.OwnerUserId),
            StartUtc = b.StartUtc,
            EndUtc = b.EndUtc,
            Status = b.Status,
            Lifecycle = b.GetLifecycle(now),
            Version = b.Version,
            SeriesId = b.SeriesId,
            Parking = b.Parking,
            CreationTime = b.CreationTime,
            LastModificationTime = b.LastModificationTime,
        }).ToList();
    }
}
