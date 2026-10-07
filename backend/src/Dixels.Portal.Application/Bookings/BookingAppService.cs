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

/* Each action needs its own-booking permission (Create / Edit / Delete). Acting on someone else's booking also
 * needs the matching "everyone's" permission (EditAll / DeleteAll); that depends on the booking, so it is checked
 * in code (EnsureCanActAsync). Seeing other people's bookings and names needs ViewAll.
 *
 * People the owner invites see the booking as their own on their schedule, but the only thing they can do with it
 * is answer: accept, tentative or decline (RespondAsync). Once declined it leaves their schedule. */
[Authorize]
public class BookingAppService : PortalAppService, IBookingAppService
{
    private const int PeopleListSize = 20;
    private const string PeopleSorting = nameof(IdentityUser.Name) + ", " + nameof(IdentityUser.Surname) + ", " + nameof(IdentityUser.UserName);

    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<BookingAttendee, Guid> _attendees;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;
    private readonly BookingManager _manager;
    private readonly BookingAttendeeManager _attendeeManager;
    private readonly MaintenanceScopeResolver _scopes;
    private readonly BookingNotifier _notifier;
    private readonly IBookingLocks _locks;

    public BookingAppService(IRepository<Booking, Guid> bookings, IRepository<BookingAttendee, Guid> attendees,
        IRepository<Space, Guid> spaces, IIdentityUserRepository users, BookingManager manager,
        BookingAttendeeManager attendeeManager, MaintenanceScopeResolver scopes, BookingNotifier notifier, IBookingLocks locks)
    {
        _bookings = bookings;
        _attendees = attendees;
        _spaces = spaces;
        _users = users;
        _manager = manager;
        _attendeeManager = attendeeManager;
        _scopes = scopes;
        _notifier = notifier;
        _locks = locks;
    }

    /* With Bookings.ViewAll you list anyone's bookings; everyone else only ever gets their own and the ones they are
     * invited to, whatever owner they ask for. Other people's time comes from GetBusyListAsync instead. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input)
    {
        var ownerId = await SeesAllBookingsAsync() ? input.OwnerUserId : CurrentUser.GetId();
        var query = await _bookings.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(b => b.Status == BookingStatus.Confirmed);
        if (input.SpaceId.HasValue) query = query.Where(b => b.SpaceId == input.SpaceId.Value);
        query = await OnSpacesOfAsync(query, input.BuildingId, input.FloorId);
        if (ownerId.HasValue)
        {
            var invited = await InvitedBookingIdsAsync(ownerId.Value);
            query = query.Where(b => b.OwnerUserId == ownerId.Value || invited.Contains(b.Id));
        }
        if (input.FromUtc.HasValue) query = query.Where(b => b.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(b => b.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(b => b.StartUtc));
        return new ListResultDto<BookingDto>(await MapListAsync(list));
    }

    /* Without Bookings.ViewAll, a booking that is neither yours nor one you're invited to is "not found", exactly like
     * a booking that doesn't exist. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<BookingDto> GetAsync(Guid id)
    {
        var b = await GetBookingAsync(id);
        if (b.OwnerUserId != CurrentUser.Id && !await SeesAllBookingsAsync() && await FindMyInviteAsync(b.Id) == null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotFound, message: L["Error:BookingNotFound"]);
        return await MapAsync(b);
    }

    /* Only when and where: enough to show a grey "Busy" block and to know a time is taken. Bookings you're invited
     * to come with your own list, so they aren't repeated here; one you declined is busy time like any other. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<ListResultDto<BusyWindowDto>> GetBusyListAsync(BusyListFilterDto input)
    {
        var me = CurrentUser.Id;
        var invited = await InvitedBookingIdsAsync(me);
        var query = (await _bookings.GetQueryableAsync())
            .Where(b => b.Status == BookingStatus.Confirmed && b.OwnerUserId != me && !invited.Contains(b.Id));
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

    /* A subquery (not run on its own): the bookings this user is invited to and hasn't declined. A declined booking
     * leaves their schedule and shows as busy like anyone else's; they can still open it (GetAsync) and change
     * their answer. */
    private async Task<IQueryable<Guid>> InvitedBookingIdsAsync(Guid? userId)
        => (await _attendees.GetQueryableAsync())
            .Where(a => a.UserId == userId && a.Response != AttendeeResponse.Declined)
            .Select(a => a.BookingId);

    private async Task<BookingAttendee?> FindMyInviteAsync(Guid bookingId)
    {
        var me = CurrentUser.Id;
        return me == null ? null : await _attendees.FirstOrDefaultAsync(a => a.BookingId == bookingId && a.UserId == me);
    }

    [Authorize(PortalPermissions.Bookings.Create)]
    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        var me = CurrentUser.GetId();
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey;
        /* Before the key lookup too: a retry that arrives while the first attempt is still saving waits for it here,
         * then finds its booking below. */
        await LockAsync(me, input.SpaceId);

        /* A retried request (same person, same key) gets the booking the first attempt made, not a second one.
         * Keys are only unique per person, so someone else's key never hands out their booking. */
        if (key != null)
        {
            var existing = await FindByKeyAsync(me, key);
            if (existing != null) return await MapAsync(existing);
        }

        var booking = await NewBookingAsync(input.SpaceId, input.StartUtc, input.EndUtc, null, key);
        var invited = await ResolveAttendeesAsync(input.SpaceId, me, input.Attendees);
        try
        {
            await InsertAsync(booking);
        }
        /* The same key saved at the same moment anyway (the database's unique index on owner + key refused this
         * one): answer with the booking that won, as for any retry. DeleteAsync only drops the refused insert
         * from this request, so it isn't tried again when the request ends. */
        catch (Exception ex) when (key != null && BookingOverlap.IsDuplicateKey(ex))
        {
            await _bookings.DeleteAsync(booking);
            var first = await FindByKeyAsync(me, key);
            if (first == null) throw;
            return await MapAsync(first);
        }
        await InviteAsync([booking], invited);
        await _notifier.BookingsCreatedAsync([booking]);
        return await MapAsync(booking);
    }

    private Task<Booking?> FindByKeyAsync(Guid ownerId, string key)
        => _bookings.FirstOrDefaultAsync(b => b.OwnerUserId == ownerId && b.IdempotencyKey == key);

    /* The client expands the recurrence rule; each surviving occurrence is created under one series.
     * An occurrence that breaks a rule is skipped and reported; the rest are still booked. */
    [Authorize(PortalPermissions.Bookings.Create)]
    public async Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input)
    {
        var seriesId = input.Occurrences.Count > 1 ? GuidGenerator.Create() : (Guid?)null;
        var created = new List<Booking>();
        var skipped = new List<BookingWindowFailureDto>();
        var invited = await ResolveAttendeesAsync(input.SpaceId, CurrentUser.GetId(), input.Attendees);
        await LockAsync(CurrentUser.GetId(), input.SpaceId);
        foreach (var o in input.Occurrences.OrderBy(o => o.StartUtc))
        {
            try
            {
                var booking = await NewBookingAsync(input.SpaceId, o.StartUtc, o.EndUtc, seriesId, null);
                await InsertAsync(booking);
                created.Add(booking);
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

        /* One confirmation listing every date that was booked, and one invitation per person invited. */
        await InviteAsync(created, invited);
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
        await LockAsync(b.OwnerUserId, b.SpaceId);
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
    public async Task<BookingDto> CancelAsync(Guid id, CancellationMessageDto? message = null)
    {
        var b = await GetBookingAsync(id);
        if (await CancelOneAsync(b)) await _notifier.BookingsCancelledAsync([b], message: message);
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
                return await CancelAsync(id, input.Message);
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
        await _notifier.BookingsCancelledAsync(cancelled, message: input.Message);
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
    public async Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input, CancellationMessageDto? message = null)
    {
        var upcoming = await AsyncExecuter.ToListAsync(await UpcomingInScopeAsync(input));
        foreach (var b in upcoming) b.Cancel();
        await _bookings.UpdateManyAsync(upcoming, autoSave: true);
        /* Each person gets one email listing their cancelled bookings. */
        await _notifier.BookingsCancelledAsync(upcoming, message: message);
        return new CancelUpcomingResultDto { CancelledCount = upcoming.Count };
    }

    /* The owner (or someone with EditAll) sets who is invited, until the booking is over. */
    [Authorize(PortalPermissions.Bookings.Edit)]
    public async Task<BookingDto> SetAttendeesAsync(Guid id, SetBookingAttendeesDto input)
    {
        var b = await GetBookingAsync(id);
        await EnsureCanActAsync(b, PortalPermissions.Bookings.EditAll, L["Error:OnlyOwnInvite"]);
        EnsureNotOver(b);
        var current = await _attendees.GetListAsync(a => a.BookingId == b.Id);
        var wanted = await ResolveAttendeesAsync(b.SpaceId, b.OwnerUserId, input.Attendees, current);
        var (added, removed) = _attendeeManager.Diff(b.Id, current, wanted);
        if (removed.Count > 0) await _attendees.DeleteManyAsync(removed, autoSave: true);
        if (added.Count > 0) await _attendees.InsertManyAsync(added, autoSave: true);
        await _notifier.AttendeesChangedAsync(b, added, removed);
        return await MapAsync(b);
    }

    /* Answering an invitation, as in Teams: accepted, tentative or declined, for this date and, with wholeSeries, for
     * the series' later dates still to come that they're invited to. Declining keeps them on the list (they can change
     * their mind) but takes the booking off their schedule and frees their seat. The owner gets an email when the
     * answer changes; giving the same answer again changes nothing and sends nothing. Accepting again after declining
     * is allowed even if the room has filled up since: the owner chose to invite them. Needs no booking permission
     * beyond seeing bookings: it only ever changes your own answer. */
    [Authorize(PortalPermissions.Bookings.Default)]
    public async Task<BookingDto> RespondAsync(Guid id, RespondToBookingDto input)
    {
        if (!AttendeeResponseExtensions.TryParseAnswer(input.Response, out var response))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingInvalidResponse, message: L["Error:InvalidResponse"]).ForField("response");
        var me = CurrentUser.GetId();
        var b = await GetBookingAsync(id);
        var invite = await FindMyInviteAsync(b.Id)
            ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingNotAttendee, message: L["Error:NotAnAttendee"]);
        EnsureNotOver(b);

        var answered = new List<(Booking Booking, BookingAttendee Row)> { (b, invite) };
        if (input.WholeSeries && b.SeriesId.HasValue)
        {
            var now = Clock.Now;
            var later = (await _bookings.GetListAsync(x => x.SeriesId == b.SeriesId && x.Id != b.Id && x.StartUtc > b.StartUtc))
                .Where(x => x.GetLifecycle(now) is TimeWindowState.Scheduled or TimeWindowState.InProgress)
                .ToDictionary(x => x.Id);
            var laterIds = later.Keys.ToList();
            var mine = await _attendees.GetListAsync(a => a.UserId == me && laterIds.Contains(a.BookingId));
            answered.AddRange(mine.Select(a => (later[a.BookingId], a)));
        }

        var changed = answered.Where(x => x.Row.Respond(response, Clock.Now)).ToList();
        if (changed.Count > 0)
        {
            await _attendees.UpdateManyAsync(changed.Select(x => x.Row), autoSave: true);
            await _notifier.AttendeeRespondedAsync(changed.Select(x => x.Booking).ToList(), me, response);
        }
        return await MapAsync(b);
    }

    /* Anyone who may book may invite, so they can search active people without seeing the admin user list. */
    [Authorize(PortalPermissions.Bookings.Create)]
    public async Task<ListResultDto<BookingPersonDto>> GetPeopleAsync(BookingPeopleFilterDto input)
    {
        var filter = string.IsNullOrWhiteSpace(input.Filter) ? null : input.Filter.Trim();
        var users = await _users.GetListAsync(PeopleSorting, PeopleListSize + 1, 0, filter, notActive: false);
        return new ListResultDto<BookingPersonDto>(users
            .Where(u => u.Id != CurrentUser.Id)
            .Take(PeopleListSize)
            .Select(u => new BookingPersonDto { Id = u.Id, Name = u.GetDisplayName(), Email = u.Email })
            .ToList());
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

    /* Who is invited can change until the booking has ended or been cancelled. */
    private void EnsureNotOver(Booking b)
    {
        var state = b.GetLifecycle(Clock.Now);
        if (state is TimeWindowState.Ended or TimeWindowState.Cancelled)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingLocked, message: L[$"Error:BookingLocked:{state.ToApiValue()}"]);
    }

    private async Task<List<AttendeeKey>> ResolveAttendeesAsync(Guid spaceId, Guid ownerId, List<AttendeeInputDto>? people,
        IReadOnlyCollection<BookingAttendee>? current = null)
    {
        if (people == null || people.Count == 0) return new();
        var ctx = await _manager.GetSpaceContextAsync(spaceId);
        return await _attendeeManager.ResolveAsync(ctx, ownerId, people.Select(p => (p.UserId, p.Email)).ToList(), current);
    }

    /* Rows only; the invitation emails go out with the booking confirmation (BookingNotifier.BookingsCreatedAsync). */
    private async Task InviteAsync(IReadOnlyCollection<Booking> bookings, IReadOnlyCollection<AttendeeKey> people)
    {
        if (bookings.Count == 0 || people.Count == 0) return;
        await _attendees.InsertManyAsync(bookings.SelectMany(b => people.Select(k => _attendeeManager.New(b.Id, k))), autoSave: true);
    }

    /* The owner's bookings and the space's bookings and blocked time are checked next, so no other request may
     * change them until this one is saved (see IBookingLocks). */
    private async Task LockAsync(Guid ownerId, Guid spaceId)
    {
        await _locks.LockOwnerAsync(ownerId);
        await _locks.LockSpacesAsync([spaceId]);
    }

    /* Runs every booking rule; nothing is saved yet. */
    private async Task<Booking> NewBookingAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? seriesId, string? idempotencyKey)
        /* Only with Bookings.MultipleSpaces may the booker hold several spaces at the same time. */
        => await _manager.CreateAsync(spaceId, CurrentUser.GetId(), startUtc.AsUtc(), endUtc.AsUtc(),
            seriesId: seriesId, idempotencyKey: idempotencyKey, ownerMayHoldSeveralSpaces: await AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.MultipleSpaces));

    /* The database has the last word on overlaps; a same-moment loser gets the normal conflict. */
    private Task InsertAsync(Booking booking) => BookingOverlap.Translate(() => _bookings.InsertAsync(booking, autoSave: true));

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

    /* ObjectMapper copies the booking; the space names, attendees and people's names come from one query each for
     * the whole list. Only with Bookings.ViewAll does anyone see who booked what; everyone else sees the names on
     * their own bookings and the ones they're invited to, and "Booked" (HiddenOwnerName, in their language) on the
     * rest, so another person's name never leaves the server. Guests' addresses are for the owner (and ViewAll) only. */
    private async Task<List<BookingDto>> MapListAsync(List<Booking> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.GetName());
        var bookingIds = list.Select(b => b.Id).ToList();
        var attendees = (await _attendees.GetListAsync(a => bookingIds.Contains(a.BookingId))).ToLookup(a => a.BookingId);
        var userIds = list.Select(b => b.OwnerUserId)
            .Concat(attendees.SelectMany(g => g).Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value))
            .Distinct().ToList();
        var users = (await _users.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id);
        var now = Clock.Now;
        var me = CurrentUser.Id;
        var seesNames = await SeesAllBookingsAsync();
        return list.Select(b =>
        {
            var dto = ObjectMapper.Map<Booking, BookingDto>(b);
            var invited = attendees[b.Id].ToList();
            var isOwner = b.OwnerUserId == me;
            dto.SpaceName = spaces.GetValueOrDefault(b.SpaceId) ?? L["UnknownSpace"];
            dto.OwnerName = seesNames || isOwner || invited.Any(a => a.UserId == me)
                ? users.GetValueOrDefault(b.OwnerUserId)?.GetDisplayName() ?? L["FormerUser"]
                : L["HiddenOwnerName"];
            dto.Lifecycle = b.GetLifecycle(now).ToApiValue();
            dto.Attendees = invited.Where(a => a.UserId.HasValue).Select(a =>
            {
                var user = users.GetValueOrDefault(a.UserId!.Value);
                return new BookingAttendeeDto
                {
                    UserId = a.UserId.Value, Name = user?.GetDisplayName() ?? L["FormerUser"], Email = user?.Email,
                    Response = a.Response.ToApiValue(), RespondedAt = a.RespondedAt,
                };
            }).OrderBy(a => a.Name).ToList();
            var guests = invited.Where(a => a.IsGuest).ToList();
            dto.GuestCount = guests.Count;
            dto.Guests = seesNames || isOwner
                ? guests.Where(a => a.Email != null).OrderBy(a => a.Email)
                    .Select(a => new BookingGuestDto { Email = a.Email!, Response = a.Response.ToApiValue(), RespondedAt = a.RespondedAt }).ToList()
                : new();
            return dto;
        }).ToList();
    }
}
