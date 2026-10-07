using System;
using Volo.Abp.Domain.Entities;

namespace Dixels.Portal.Bookings;

/* Someone the owner invited to a booking. Either a portal user (UserId; their name and email are looked up when
 * needed) or an outside guest known only by email address (UserId is null).
 *
 * A guest's address is kept only while the booking can still change, so they hear about a new time or a cancellation.
 * Once the booking is over or cancelled it is wiped (ForgetGuestEmail) and the row stays, so the booking still
 * counts its guests. */
public class BookingAttendee : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid? UserId { get; private set; }
    /* Guests only, stored lower-case; null once forgotten. */
    public string? Email { get; private set; }
    /* Their answer, as in Teams: None until they reply. Only portal users answer in the portal; a guest's reply goes
     * from their own calendar straight to the owner, so a guest stays None here. */
    public AttendeeResponse Response { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    public bool IsGuest => UserId == null;
    /* Someone who declined stays on the list (they may change their mind) but takes no seat and doesn't have the
     * booking on their schedule. */
    public bool HasDeclined => Response == AttendeeResponse.Declined;

    protected BookingAttendee() { }

    private BookingAttendee(Guid id, Guid bookingId, Guid? userId, string? email) : base(id)
    {
        BookingId = bookingId;
        UserId = userId;
        Email = email;
    }

    public static BookingAttendee ForUser(Guid id, Guid bookingId, Guid userId) => new(id, bookingId, userId, null);

    public static BookingAttendee ForGuest(Guid id, Guid bookingId, string email) => new(id, bookingId, null, email);

    public void ForgetGuestEmail()
    {
        if (IsGuest) Email = null;
    }

    /* False when it is the answer they had already given: nothing changes and nobody is told again. */
    public bool Respond(AttendeeResponse response, DateTime at)
    {
        if (response == Response) return false;
        Response = response;
        RespondedAt = at;
        return true;
    }

    public bool Is(AttendeeKey key) => key.UserId.HasValue ? UserId == key.UserId : UserId == null && Email == key.Email;
}

/* Who an attendee is, before or apart from a row: a user id, or a guest's lower-case email address. */
public record AttendeeKey(Guid? UserId, string? Email)
{
    public static AttendeeKey User(Guid userId) => new(userId, null);
    public static AttendeeKey Guest(string email) => new(null, email);
}
