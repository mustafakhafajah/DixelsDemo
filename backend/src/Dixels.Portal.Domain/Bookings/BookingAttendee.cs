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

    public bool IsGuest => UserId == null;

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

    public bool Is(AttendeeKey key) => key.UserId.HasValue ? UserId == key.UserId : UserId == null && Email == key.Email;
}

/* Who an attendee is, before or apart from a row: a user id, or a guest's lower-case email address. */
public record AttendeeKey(Guid? UserId, string? Email)
{
    public static AttendeeKey User(Guid userId) => new(userId, null);
    public static AttendeeKey Guest(string email) => new(null, email);
}
