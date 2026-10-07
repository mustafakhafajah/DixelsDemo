using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Dixels.Portal.Bookings;

/* The public answer page for outside guests. No sign-in: the private secret in their invitation's links is the
 * permission, and it only reaches that guest's own answer, on bookings that haven't ended or been cancelled.
 * The secret is compared by its hash (BookingAttendee.HashToken), and it stops working once the booking is over and
 * the guest's address is wiped. */
[AllowAnonymous]
public class BookingInvitationAppService : PortalAppService, IBookingInvitationAppService
{
    private readonly IRepository<BookingAttendee, Guid> _attendees;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IIdentityUserRepository _users;
    private readonly BookingManager _manager;
    private readonly BookingNotifier _notifier;

    public BookingInvitationAppService(IRepository<BookingAttendee, Guid> attendees, IRepository<Booking, Guid> bookings,
        IIdentityUserRepository users, BookingManager manager, BookingNotifier notifier)
    {
        _attendees = attendees;
        _bookings = bookings;
        _users = users;
        _manager = manager;
        _notifier = notifier;
    }

    public async Task<BookingInvitationDto> GetAsync(string token)
    {
        var (row, booking) = await FindAsync(token);
        return await MapAsync(row, booking);
    }

    public async Task<BookingInvitationDto> RespondAsync(string token, RespondToBookingDto input)
    {
        if (!AttendeeResponseExtensions.TryParseAnswer(input.Response, out var response))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingInvalidResponse, message: L["Error:InvalidResponse"]).ForField("response");
        var (row, booking) = await FindAsync(token);
        var state = booking.GetLifecycle(Clock.Now);
        if (state is TimeWindowState.Ended or TimeWindowState.Cancelled)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingLocked, message: L[$"Error:BookingLocked:{state.ToApiValue()}"]);

        var answered = new List<(Booking Booking, BookingAttendee Row)> { (booking, row) };
        /* The same guest (same address) on the series' later dates that are still to come. */
        if (input.WholeSeries && booking.SeriesId.HasValue)
        {
            var now = Clock.Now;
            var later = (await _bookings.GetListAsync(x => x.SeriesId == booking.SeriesId && x.Id != booking.Id && x.StartUtc > booking.StartUtc))
                .Where(x => x.GetLifecycle(now) is TimeWindowState.Scheduled or TimeWindowState.InProgress)
                .ToDictionary(x => x.Id);
            var laterIds = later.Keys.ToList();
            var email = row.Email;
            var same = await _attendees.GetListAsync(a => a.UserId == null && a.Email == email && laterIds.Contains(a.BookingId));
            answered.AddRange(same.Select(a => (later[a.BookingId], a)));
        }

        var changed = answered.Where(x => x.Row.Respond(response, Clock.Now)).ToList();
        if (changed.Count > 0)
        {
            await _attendees.UpdateManyAsync(changed.Select(x => x.Row), autoSave: true);
            await _notifier.GuestRespondedAsync(changed.Select(x => x.Booking).ToList(), row.Email!, response);
        }
        return await MapAsync(row, booking);
    }

    /* An unknown, replaced or wiped secret reads as "not found", the same as a booking that doesn't exist. */
    private async Task<(BookingAttendee Row, Booking Booking)> FindAsync(string token)
    {
        var hash = string.IsNullOrWhiteSpace(token) || token.Length > 128 ? null : BookingAttendee.HashToken(token.Trim());
        var row = hash == null ? null : await _attendees.FirstOrDefaultAsync(a => a.ResponseTokenHash == hash);
        var booking = row?.Email == null ? null : await _bookings.FindAsync(row.BookingId);
        if (row == null || booking == null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingInvitationNotFound, message: L["Error:InvitationNotFound"]);
        return (row, booking);
    }

    private async Task<BookingInvitationDto> MapAsync(BookingAttendee row, Booking booking)
    {
        var owner = await _users.FindAsync(booking.OwnerUserId);
        SpaceContext? ctx = null;
        try { ctx = await _manager.GetSpaceContextAsync(booking.SpaceId); }
        catch (UserFriendlyException) { /* a space since removed: the page still shows the time and the answer */ }
        return new BookingInvitationDto
        {
            SpaceName = ctx?.Space.GetName() ?? L["UnknownSpace"],
            BuildingName = ctx?.Building.GetName() ?? "",
            FloorName = ctx?.Floor?.GetName(),
            StartUtc = booking.StartUtc,
            EndUtc = booking.EndUtc,
            TimeZone = ctx?.Building.TimeZone ?? "UTC",
            OwnerName = owner?.GetDisplayName() ?? L["FormerUser"],
            Response = row.Response.ToApiValue(),
            Lifecycle = booking.GetLifecycle(Clock.Now).ToApiValue(),
        };
    }
}
