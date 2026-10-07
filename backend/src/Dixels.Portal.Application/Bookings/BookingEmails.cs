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

    private static string List(IEnumerable<BookingEmailLine> lines)
        => "<ul style=\"margin:0;padding-left:20px\">"
           + string.Concat(lines.OrderBy(l => l.StartUtc).Select(l => $"<li style=\"margin-bottom:4px\">{Encode(Describe(l))}</li>"))
           + "</ul>";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
}
