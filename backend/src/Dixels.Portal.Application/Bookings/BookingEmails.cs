using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using static Dixels.Portal.Common.PortalEmailLayout;

namespace Dixels.Portal.Bookings;

/* One booking as an email shows it: where, and when in the building's own time zone. */
public record BookingEmailLine(string SpaceName, string Place, DateTime StartUtc, DateTime EndUtc, string TimeZoneId);

/* The booking emails, in English (the portal's emails are English only). Pure: the same input gives the same email,
 * so they are tested without a database. */
public static class BookingEmails
{
    public static EmailContent Confirmed(string name, IReadOnlyList<BookingEmailLine> lines, string? bookingsUrl)
    {
        var subject = lines.Count == 1
            ? $"Booking confirmed: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{lines.Count} bookings confirmed: {lines[0].SpaceName}";
        var intro = lines.Count == 1 ? "Your booking is confirmed:" : $"Your {lines.Count} bookings are confirmed:";
        return new EmailContent(subject, Html("Booking confirmed",
            [$"Hello {Encode(name)},", Encode(intro), List(lines), Encode("You'll get a reminder 10 minutes before each one starts.")],
            "View my bookings", bookingsUrl));
    }

    public static EmailContent Rescheduled(string name, BookingEmailLine before, BookingEmailLine after, string? bookingsUrl)
        => new($"Booking changed: {after.SpaceName}, {Day(after)}", Html("Booking changed",
            [$"Hello {Encode(name)},", "Your booking has a new time:",
             $"<strong>Now:</strong> {Encode(Describe(after))}",
             $"<span style=\"color:#6e7781\"><s>Was: {Encode(Describe(before))}</s></span>"],
            "View my bookings", bookingsUrl));

    /* cancelledByOther: someone else (an admin) cancelled them; reason: why, when there is one (e.g. blocked time). */
    public static EmailContent Cancelled(string name, IReadOnlyList<BookingEmailLine> lines, bool cancelledByOther, string? reason, string? bookingsUrl)
    {
        var subject = lines.Count == 1
            ? $"Booking cancelled: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{lines.Count} bookings cancelled";
        var what = lines.Count == 1 ? "booking has" : $"{lines.Count} bookings have";
        var who = cancelledByOther ? $"Your {what} been cancelled by an administrator" : $"Your {what} been cancelled";
        var paragraphs = new List<string> { $"Hello {Encode(name)},", Encode(who + (reason != null ? $" ({reason})." : ".")), List(lines) };
        if (cancelledByOther) paragraphs.Add(Encode("You can book another time or space in the portal."));
        return new EmailContent(subject, Html(lines.Count == 1 ? "Booking cancelled" : "Bookings cancelled", paragraphs, "Open the portal", bookingsUrl));
    }

    public static EmailContent Reminder(string name, BookingEmailLine line, string? bookingsUrl)
        => new($"Starting soon: {line.SpaceName} at {LocalTime(line.StartUtc, line.TimeZoneId)}", Html("Your booking starts soon",
            [$"Hello {Encode(name)},", "Your booking starts in about 10 minutes:", $"<strong>{Encode(Describe(line))}</strong>"],
            "View my bookings", bookingsUrl));

    /* To someone invited by ownerName; name is null for an outside guest. A portal user is attending unless they
     * refuse, so they get Accept (opens the booking) and Refuse (takes them off it); a guest gets the details only. */
    public static EmailContent Invited(string? name, string ownerName, IReadOnlyList<BookingEmailLine> lines, string? acceptUrl, string? refuseUrl)
    {
        var subject = lines.Count == 1
            ? $"Invitation: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"Invitation: {lines.Count} bookings in {lines[0].SpaceName}";
        var intro = lines.Count == 1 ? $"{ownerName} has invited you to a booking:" : $"{ownerName} has invited you to {lines.Count} bookings:";
        var paragraphs = new List<string> { Hello(name), Encode(intro), List(lines) };
        var canRefuse = !string.IsNullOrWhiteSpace(refuseUrl);
        if (canRefuse) paragraphs.Add(Encode("You're down as attending. If you can't make it, choose Refuse and you'll be taken off the booking."));
        return new EmailContent(subject, Html("You're invited", paragraphs,
            canRefuse ? "Accept" : null, acceptUrl, canRefuse ? "Refuse" : null, refuseUrl));
    }

    /* To someone the owner took off the list. */
    public static EmailContent Uninvited(string? name, string ownerName, IReadOnlyList<BookingEmailLine> lines)
    {
        var subject = lines.Count == 1
            ? $"No longer invited: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"No longer invited: {lines.Count} bookings";
        var intro = lines.Count == 1 ? $"{ownerName} has taken you off this booking:" : $"{ownerName} has taken you off these bookings:";
        return new EmailContent(subject, Html("You're no longer invited",
            [Hello(name), Encode(intro), List(lines), Encode("You don't need to do anything.")]));
    }

    /* To the people invited when the owner moves the booking. */
    public static EmailContent InviteRescheduled(string? name, string ownerName, BookingEmailLine before, BookingEmailLine after, string? bookingUrl)
        => new($"Booking changed: {after.SpaceName}, {Day(after)}", Html("Booking changed",
            [Hello(name), Encode($"{ownerName}'s booking you're invited to has a new time:"),
             $"<strong>Now:</strong> {Encode(Describe(after))}",
             $"<span style=\"color:#6e7781\"><s>Was: {Encode(Describe(before))}</s></span>"],
            bookingUrl != null ? "View the booking" : null, bookingUrl));

    /* To the people invited when the booking is cancelled, by the owner or anyone else. */
    public static EmailContent InviteCancelled(string? name, string ownerName, IReadOnlyList<BookingEmailLine> lines, string? reason)
    {
        var subject = lines.Count == 1
            ? $"Booking cancelled: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{lines.Count} bookings cancelled";
        var what = lines.Count == 1 ? "booking you were invited to has" : $"{lines.Count} bookings you were invited to have";
        return new EmailContent(subject, Html(lines.Count == 1 ? "Booking cancelled" : "Bookings cancelled",
            [Hello(name), Encode($"{ownerName}'s {what} been cancelled" + (reason != null ? $" ({reason})." : ".")), List(lines)]));
    }

    /* To the owner when someone they invited refuses. */
    public static EmailContent AttendeeLeft(string name, string attendeeName, IReadOnlyList<BookingEmailLine> lines, string? bookingsUrl)
    {
        var subject = lines.Count == 1
            ? $"{attendeeName} can't make it: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{attendeeName} can't make it: {lines.Count} bookings";
        var what = lines.Count == 1 ? "your booking and is no longer invited" : $"{lines.Count} of your bookings and is no longer invited to them";
        return new EmailContent(subject, Html("Someone can't make it",
            [$"Hello {Encode(name)},", Encode($"{attendeeName} has refused {what}:"), List(lines)],
            "View my bookings", bookingsUrl));
    }

    /* "Room 4 · HQ North · Floor 3 — Mon 6 Oct 2026, 10:00–11:00 (Europe/Warsaw)". */
    public static string Describe(BookingEmailLine line)
        => $"{line.SpaceName}{(line.Place.Length > 0 ? " · " + line.Place : "")} — {When(line)}";

    /* The local date and times; a booking that runs past midnight shows the end date too. */
    public static string When(BookingEmailLine line)
    {
        var zone = BuildingCalendar.Zone(line.TimeZoneId);
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(line.StartUtc, DateTimeKind.Utc), zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(line.EndUtc, DateTimeKind.Utc), zone);
        var endText = end.Date == start.Date ? end.ToString("HH:mm", Invariant) : end.ToString("ddd d MMM yyyy, HH:mm", Invariant);
        return $"{start.ToString("ddd d MMM yyyy, HH:mm", Invariant)}–{endText} ({zone.Id})";
    }

    private static string Day(BookingEmailLine line)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(line.StartUtc, DateTimeKind.Utc), BuildingCalendar.Zone(line.TimeZoneId))
            .ToString("ddd d MMM", Invariant);

    private static string LocalTime(DateTime utc, string timeZoneId)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BuildingCalendar.Zone(timeZoneId)).ToString("HH:mm", Invariant);

    private static string Hello(string? name) => string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {Encode(name)},";

    private static string List(IEnumerable<BookingEmailLine> lines)
        => "<ul style=\"margin:0;padding-left:20px\">"
           + string.Concat(lines.OrderBy(l => l.StartUtc).Select(l => $"<li style=\"margin-bottom:4px\">{Encode(Describe(l))}</li>"))
           + "</ul>";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
}
