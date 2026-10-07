using System;
using System.Security.Cryptography;
using System.Text;
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
    /* Their answer, as in Teams: None until they reply, from the Accept / Tentative / Decline buttons of the
     * invitation email (portal users through the portal, guests through their private link). */
    public AttendeeResponse Response { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    /* Guests only: the SHA-256 (hex) of the secret in their invitation's answer links. Only the hash is kept, like a
     * password, so the database alone can't be used to answer for them; wiped with the email address. */
    public string? ResponseTokenHash { get; private set; }

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
        if (!IsGuest) return;
        Email = null;
        ResponseTokenHash = null;
    }

    /* A new secret for a guest's answer links (an older one stops working); the caller puts it in the email. */
    public string IssueResponseToken()
    {
        if (!IsGuest) throw new InvalidOperationException("Only guests answer through a private link.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        ResponseTokenHash = HashToken(token);
        return token;
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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
