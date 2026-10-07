using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Identity;

namespace Dixels.Portal.Bookings;

/* The rules for who can be invited to a booking. Each person is a user id or an email address; an address that
 * belongs to an active user is treated as that user, so they see the booking on their schedule like anyone picked
 * from the list. */
public class BookingAttendeeManager : PortalDomainService
{
    private readonly IIdentityUserRepository _users;
    private readonly ILookupNormalizer _normalizer;

    public BookingAttendeeManager(IIdentityUserRepository users, ILookupNormalizer normalizer)
    {
        _users = users;
        _normalizer = normalizer;
    }

    /* The checked list, in the order given. Refuses an unknown or inactive user, the owner, a bad address, anyone
     * listed twice, and more people than the room seats (the owner counts as one; a capacity of 0 means not set).
     * current: the booking's rows today, when its list is being changed. Someone still on the list who declined keeps
     * that answer (see Diff), so they take no seat. */
    public async Task<List<AttendeeKey>> ResolveAsync(SpaceContext ctx, Guid ownerId, IReadOnlyList<(Guid? UserId, string? Email)> people,
        IReadOnlyCollection<BookingAttendee>? current = null)
    {
        if (people.Count > BookingConsts.MaxAttendees)
            throw Invalid(L["Error:TooManyAttendees", BookingConsts.MaxAttendees]);

        var ids = people.Where(p => p.UserId.HasValue).Select(p => p.UserId!.Value).Distinct().ToList();
        var users = ids.Count == 0 ? new Dictionary<Guid, IdentityUser>()
            : (await _users.GetListByIdsAsync(ids)).ToDictionary(u => u.Id);

        var keys = new List<AttendeeKey>();
        foreach (var (userId, email) in people)
        {
            var key = userId.HasValue
                ? users.TryGetValue(userId.Value, out var user) && user.IsActive
                    ? AttendeeKey.User(user.Id)
                    : throw Invalid(L["Error:AttendeeNotFound"])
                : await KeyForEmailAsync(email);
            if (key.UserId == ownerId) throw Invalid(L["Error:AttendeeIsOwner"]);
            if (keys.Contains(key))
                throw Invalid(L["Error:AttendeeTwice", key.Email ?? users.GetValueOrDefault(key.UserId!.Value)?.Email ?? ""]);
            keys.Add(key);
        }

        var capacity = ctx.Space.Capacity;
        var seats = keys.Count(k => current == null || !current.Any(a => a.HasDeclined && a.Is(k))) + 1;
        if (capacity > 0 && seats > capacity)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingOverCapacity,
                message: L["Error:BookingOverCapacity", ctx.Space.GetName(), capacity, seats]).ForField("attendees");
        return keys;
    }

    /* The rows to add and the rows to remove so a booking's attendees become exactly the wanted list.
     * People already on it keep their row, and with it their answer. */
    public (List<BookingAttendee> Added, List<BookingAttendee> Removed) Diff(Guid bookingId,
        IReadOnlyCollection<BookingAttendee> current, IReadOnlyCollection<AttendeeKey> wanted)
    {
        var added = wanted.Where(k => !current.Any(a => a.Is(k))).Select(k => New(bookingId, k)).ToList();
        var removed = current.Where(a => !wanted.Any(a.Is)).ToList();
        return (added, removed);
    }

    public BookingAttendee New(Guid bookingId, AttendeeKey key)
        => key.UserId.HasValue
            ? BookingAttendee.ForUser(GuidGenerator.Create(), bookingId, key.UserId.Value)
            : BookingAttendee.ForGuest(GuidGenerator.Create(), bookingId, key.Email!);

    private async Task<AttendeeKey> KeyForEmailAsync(string? email)
    {
        var address = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(address)) throw Invalid(L["Error:AttendeeMissing"]);
        if (!IsEmail(address)) throw Invalid(L["Error:AttendeeBadEmail", address]);
        var user = await _users.FindByNormalizedEmailAsync(_normalizer.NormalizeEmail(address));
        return user is { IsActive: true } ? AttendeeKey.User(user.Id) : AttendeeKey.Guest(address);
    }

    /* One @, something on each side, a dot in the domain, no spaces; and short enough to store. */
    private static bool IsEmail(string address)
    {
        if (address.Length > BookingConsts.MaxAttendeeEmailLength || address.Any(char.IsWhiteSpace)) return false;
        var at = address.IndexOf('@');
        if (at <= 0 || at != address.LastIndexOf('@') || !address[(at + 1)..].Contains('.')) return false;
        return MailAddress.TryCreate(address, out var parsed) && parsed.Address == address;
    }

    private static UserFriendlyException Invalid(string message)
        => new UserFriendlyException(code: PortalDomainErrorCodes.BookingInvalidAttendee, message: message).ForField("attendees");
}
