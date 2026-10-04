using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Notifications;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Bookings;

/* Each action needs its own-booking permission (Create / Edit / Delete). Acting on someone else's booking also
 * needs the matching "everyone's" permission (EditAll / DeleteAll); that depends on the booking, so it is checked
 * in code (EnsureCanActAsync). Seeing other people's bookings and names needs ViewAll. */
[Authorize]
public class BookingAppService : PortalAppService, IBookingAppService
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;
    private readonly BookingManager _manager;
    private readonly MaintenanceScopeResolver _scopes;
    private readonly BookingNotifier _notifier;

    public BookingAppService(IRepository<Booking, Guid> bookings, IRepository<Space, Guid> spaces,
        IIdentityUserRepository users, BookingManager manager, MaintenanceScopeResolver scopes, BookingNotifier notifier)
    {
        _bookings = bookings;
        _spaces = spaces;
        _users = users;
        _manager = manager;
        _scopes = scopes;
        _notifier = notifier;
    }

    /* With Bookings.ViewAll you list anyone's bookings; everyone else only ever gets their own,
     * whatever owner they ask for. Other people's time comes from GetBusyListAsync instead. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input)
    {
        var ownerId = await SeesAllBookingsAsync() ? input.OwnerUserId : CurrentUser.GetId();
        var query = await _bookings.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(b => b.Status == BookingStatus.Confirmed);
        if (input.SpaceId.HasValue) query = query.Where(b => b.SpaceId == input.SpaceId.Value);
        query = await OnSpacesOfAsync(query, input.BuildingId, input.FloorId);
        if (ownerId.HasValue) query = query.Where(b => b.OwnerUserId == ownerId.Value);
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc));
        return new ListResultDto<BookingDto>(await MapListAsync(list));
    }

    /* Without Bookings.ViewAll, someone else's booking is "not found", exactly like a booking that doesn't exist. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<BookingDto> GetAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        if (b.OwnerUserId != CurrentUser.Id && !await SeesAllBookingsAsync())
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotFound, message: L["Error:BookingNotFound"]);
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
        query = await OnSpacesOfAsync(query, input.BuildingId, input.FloorId);
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var windows = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc)
            .Select(b => new BusyWindowDto { SpaceId = b.SpaceId, StartUtc = b.StartUtc, EndUtc = b.EndUtc }));
        return new ListResultDto<BusyWindowDto>(windows);
    }

    /* The Building / Floor filters: only bookings on spaces of that building / floor (a subquery, one round trip). */
    private async Task<IQueryable<Booking>> OnSpacesOfAsync(IQueryable<Booking> query, Guid? buildingId, Guid? floorId)
    {
        if (!buildingId.HasValue && !floorId.HasValue) return query;
        var spaces = await _spaces.GetQueryableAsync();
        if (buildingId.HasValue) spaces = spaces.Where(s => s.BuildingId == buildingId.Value);
        if (floorId.HasValue) spaces = spaces.Where(s => s.FloorId == floorId.Value);
        var spaceIds = spaces.Select(s => s.Id);
        return query.Where(b => spaceIds.Contains(b.SpaceId));
    }

    private Task<bool> SeesAllBookingsAsync() => AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.ViewAll);

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

        var booking = await CreateOneAsync(input.SpaceId, input.StartUtc, input.EndUtc, null, key);
        await _notifier.BookingsCreatedAsync([booking]);
        return await MapAsync(booking);
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
            /* Losing a same-moment race stops the whole series (see BookingRaceException); other clashes skip the date. */
            catch (BusinessException ex) when (ex is not BookingRaceException)
            {
                skipped.Add(new BookingWindowFailureDto
                {
                    StartUtc = o.StartUtc, EndUtc = o.EndUtc,
                    ErrorCode = ex.Code ?? "unknown", ErrorMessage = ex.Message,
                });
            }
        }

        /* One confirmation listing every date that was booked. */
        await _notifier.BookingsCreatedAsync(created);

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
        await EnsureCanActAsync(b, PortalPermissions.Bookings.EditAll, L["Error:OnlyOwnChange"]);
        var state = b.GetLifecycle(Clock.Now);
        if (state is TimeWindowState.Ended or TimeWindowState.Cancelled)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingLocked, message: L[$"Error:BookingLocked:{state.ToApiValue()}"]);
        if (state == TimeWindowState.InProgress)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingInProgress, message:
                L["Error:BookingInProgress"]);
        /* Optimistic concurrency: refuse if someone changed the booking after the client loaded it. */
        if (input.ExpectedVersion.HasValue && input.ExpectedVersion.Value != b.Version)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.VersionMismatch, message:
                    L["Error:VersionMismatch"])
                .WithData("expected", input.ExpectedVersion.Value)
                .WithData("current", b.Version);

        var start = input.StartUtc.AsUtc();
        var end = input.EndUtc.AsUtc();
        var ctx = await _manager.GetSpaceContextAsync(b.SpaceId);
        _manager.ValidateWindow(ctx, start, end);
        await _manager.EnsureNoMaintenanceAsync(ctx, start, end);
        await _manager.EnsureNoConflictAsync(b.SpaceId, start, end, b.Id);
        /* The owner's rule applies: moving someone else's booking still can't give them two rooms at once; only your own
         * booking skips the check, and only with Bookings.MultipleSpaces. */
        if (!(b.OwnerUserId == CurrentUser.Id && await AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.MultipleSpaces)))
            await _manager.EnsureNoSelfOverlapAsync(b.OwnerUserId, b.SpaceId, start, end, b.Id);

        var (oldStart, oldEnd) = (b.StartUtc, b.EndUtc);
        b.StartUtc = start;
        b.EndUtc = end;
        b.Version++;
        await BookingOverlap.Translate(() => _bookings.UpdateAsync(b, autoSave: true));
        await _notifier.BookingRescheduledAsync(b, oldStart, oldEnd);
        return await MapAsync(b);
    }

    [Authorize(PortalPermissions.Bookings.Delete)]
    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        if (await CancelOneAsync(b)) await _notifier.BookingsCancelledAsync([b]);
        return await MapAsync(b);
    }

    /* PATCH: the body says which change it is. Calls inside this class skip the [Authorize] checks on the
     * actions, so each change checks the same permission its own action needs. */
    public async Task<BookingDto> UpdateAsync(Guid id, UpdateBookingDto input)
    {
        switch (input.Lifecycle)
        {
            case UpdateBookingDto.Cancelled:
                await AuthorizationService.CheckAsync(PortalPermissions.Bookings.Delete);
                return await CancelAsync(id);
            case UpdateBookingDto.Ended:
                await AuthorizationService.CheckAsync(PortalPermissions.Bookings.Edit);
                return await EndEarlyAsync(id);
            default:
                await AuthorizationService.CheckAsync(PortalPermissions.Bookings.Edit);
                return await RescheduleAsync(id, new RescheduleBookingDto
                {
                    StartUtc = input.StartUtc!.Value,
                    EndUtc = input.EndUtc!.Value,
                    ExpectedVersion = input.ExpectedVersion,
                });
        }
    }

    /* Cancels the series' bookings from input.FromUtc on; ended occurrences are left alone. Every occurrence
     * has the same owner, so one ownership check covers the whole series. */
    [Authorize(PortalPermissions.Bookings.Delete)]
    public async Task<CancelSeriesResultDto> CancelSeriesAsync(Guid seriesId, CancelBookingSeriesDto input)
    {
        var series = await _bookings.GetListAsync(b => b.SeriesId == seriesId);
        if (series.Count == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotFound, message: L["Error:BookingNotFound"]);
        await EnsureCanActAsync(series[0], PortalPermissions.Bookings.DeleteAll, L["Error:OnlyOwnCancel"]);

        var from = input.FromUtc.AsUtc();
        var cancelled = new List<Booking>();
        foreach (var b in series.Where(b => b.Status == BookingStatus.Confirmed && b.StartUtc >= from
                                            && b.GetLifecycle(Clock.Now) != TimeWindowState.Ended))
        {
            if (await CancelOneAsync(b)) cancelled.Add(b);
        }
        /* One email listing every cancelled date. */
        await _notifier.BookingsCancelledAsync(cancelled);
        return new CancelSeriesResultDto { SeriesId = seriesId, CancelledCount = cancelled.Count };
    }

    /* Frees the space now: the booking's end moves to the current time. */
    [Authorize(PortalPermissions.Bookings.Edit)]
    public async Task<BookingDto> EndEarlyAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, PortalPermissions.Bookings.EditAll, L["Error:OnlyOwnEnd"]);
        if (b.GetLifecycle(Clock.Now) != TimeWindowState.InProgress)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotInProgress, message:
                L["Error:BookingNotInProgress"]);
        b.EndUtc = Clock.Now;
        b.Version++;
        await BookingOverlap.Translate(() => _bookings.UpdateAsync(b, autoSave: true));
        return await MapAsync(b);
    }

    /* How many of everyone's bookings are still to come: reading other people's bookings, so ViewAll. */
    [Authorize(PortalPermissions.Bookings.ViewAll)]
    public async Task<int> GetUpcomingCountAsync(EstateScopeDto input)
        => await AsyncExecuter.CountAsync(await UpcomingInScopeAsync(input));

    /* Cancel every upcoming booking in a scope, e.g. after making it not bookable: cancelling other people's, so DeleteAll. */
    [Authorize(PortalPermissions.Bookings.DeleteAll)]
    public async Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input)
    {
        var upcoming = await AsyncExecuter.ToListAsync(await UpcomingInScopeAsync(input));
        foreach (var b in upcoming) b.Cancel();
        await _bookings.UpdateManyAsync(upcoming, autoSave: true);
        /* Each person gets one email listing their cancelled bookings. */
        await _notifier.BookingsCancelledAsync(upcoming);
        return new CancelUpcomingResultDto { CancelledCount = upcoming.Count };
    }

    private async Task<Booking> GetBookingAsync(Guid id)
        => await _bookings.FindAsync(id)
           ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotFound, message: L["Error:BookingNotFound"]);

    /* Owners act on their own bookings; someone else's needs the given "everyone's" permission (EditAll / DeleteAll). */
    private async Task EnsureCanActAsync(Booking b, string everyonesPermission, string message)
    {
        if (b.OwnerUserId != CurrentUser.Id && !await AuthorizationService.IsGrantedAsync(everyonesPermission))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.AccessForbidden, message: message);
    }

    private async Task<Booking> CreateOneAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? seriesId, string? idempotencyKey)
    {
        /* Only with Bookings.MultipleSpaces may the booker hold several spaces at the same time. */
        var booking = await _manager.CreateAsync(spaceId, CurrentUser.GetId(), startUtc.AsUtc(), endUtc.AsUtc(),
            seriesId: seriesId, idempotencyKey: idempotencyKey, ownerMayHoldSeveralSpaces: await AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.MultipleSpaces));
        /* The database has the last word on overlaps; a same-moment loser gets the normal conflict. */
        await BookingOverlap.Translate(() => _bookings.InsertAsync(booking, autoSave: true));
        return booking;
    }

    /* False when it was already cancelled (nothing changed, so no email). */
    private async Task<bool> CancelOneAsync(Booking b)
    {
        await EnsureCanActAsync(b, PortalPermissions.Bookings.DeleteAll, L["Error:OnlyOwnCancel"]);
        var state = b.GetLifecycle(Clock.Now);
        if (state == TimeWindowState.Ended)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingLocked, message: L["Error:EndedCannotCancel"]);
        if (state == TimeWindowState.Cancelled) return false;
        b.Cancel();
        await _bookings.UpdateAsync(b, autoSave: true);
        return true;
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

    /* ObjectMapper copies the booking; the space and owner names come from one query each for the whole list.
     * Only with Bookings.ViewAll does anyone see who booked what; everyone else sees their own name and
     * "Booked" (HiddenOwnerName, in their language) on the rest, so another person's name never leaves the server. */
    private async Task<List<BookingDto>> MapListAsync(List<Booking> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.GetName());
        var userIds = list.Select(b => b.OwnerUserId).Distinct().ToList();
        var users = (await _users.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id, u => u.GetDisplayName());
        var now = Clock.Now;
        var seesNames = await SeesAllBookingsAsync();
        return list.Select(b =>
        {
            var dto = ObjectMapper.Map<Booking, BookingDto>(b);
            dto.SpaceName = spaces.GetValueOrDefault(b.SpaceId) ?? L["UnknownSpace"];
            dto.OwnerName = seesNames || b.OwnerUserId == CurrentUser.Id
                ? users.GetValueOrDefault(b.OwnerUserId) ?? L["FormerUser"]
                : L["HiddenOwnerName"];
            dto.Lifecycle = b.GetLifecycle(now).ToApiValue();
            return dto;
        }).ToList();
    }
}
