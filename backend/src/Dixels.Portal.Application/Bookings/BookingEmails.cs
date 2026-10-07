using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using static Dixels.Portal.Common.PortalEmailLayout;

namespace Dixels.Portal.Bookings;

/* One booking as an email shows it: where, and when. TimeZoneId is the reader's zone: their own choice
 * (Portal.TimeZone), or the building's when they have none (BookingNotifier decides). */
public record BookingEmailLine(string SpaceName, string Place, DateTime StartUtc, DateTime EndUtc, string TimeZoneId);

/* The Accept / Tentative / Decline buttons of an invitation: the bookings page opens the booking and records the
 * answer (signing in first keeps the link). They are the only way to answer; the portal just shows it.
 * wholeSeries: the answer covers this date and the series' later ones. */
public record ResponseLinks(string Accept, string Tentative, string Decline)
{
    public static ResponseLinks For(string bookingsUrl, Guid bookingId, bool wholeSeries = false)
    {
        var page = $"{bookingsUrl}?booking={bookingId}{(wholeSeries ? "&series=1" : "")}&respond=";
        return new ResponseLinks(page + "accept", page + "tentative", page + "decline");
    }

    /* A guest can't sign in: their buttons open the public answer page with the private secret of their invitation. */
    public static ResponseLinks ForGuest(string spaRoot, string token, bool wholeSeries = false)
    {
        var page = $"{spaRoot}/{PortalAppUrls.RespondPage}/{token}?{(wholeSeries ? "series=1&" : "")}answer=";
        return new ResponseLinks(page + "accept", page + "tentative", page + "decline");
    }
}

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

    /* cancelledByOther: someone else (an admin) cancelled them; reason: why, when there is one (e.g. blocked time).
     * subject / message: what the person cancelling wrote, if anything; the subject replaces the usual one and the
     * message comes before the list. */
    public static EmailContent Cancelled(string name, IReadOnlyList<BookingEmailLine> lines, bool cancelledByOther, string? reason, string? bookingsUrl,
        string? subject = null, string? message = null)
    {
        var what = lines.Count == 1 ? "booking has" : $"{lines.Count} bookings have";
        var who = cancelledByOther ? $"Your {what} been cancelled by an administrator" : $"Your {what} been cancelled";
        var paragraphs = new List<string> { $"Hello {Encode(name)}," };
        paragraphs.AddRange(Note(message));
        paragraphs.Add(Encode(who + (reason != null ? $" ({reason})." : ".")));
        paragraphs.Add(List(lines));
        if (cancelledByOther) paragraphs.Add(Encode("You can book another time or space in the portal."));
        return new EmailContent(CancelSubject(lines, subject),
            Html(lines.Count == 1 ? "Booking cancelled" : "Bookings cancelled", paragraphs, "Open the portal", bookingsUrl));
    }

    public static EmailContent Reminder(string name, BookingEmailLine line, string? bookingsUrl)
        => new($"Starting soon: {line.SpaceName} at {LocalTime(line.StartUtc, line.TimeZoneId)}", Html("Your booking starts soon",
            [$"Hello {Encode(name)},", "Your booking starts in about 10 minutes:", $"<strong>{Encode(Describe(line))}</strong>"],
            "View my bookings", bookingsUrl));

    /* To someone invited by ownerName; name is null for an outside guest. Everyone answers with the Accept / Maybe /
     * Decline buttons (Gmail's words): a portal user's open the portal, a guest's open their private answer page.
     * respond is null only when the portal's address isn't set. */
    public static EmailContent Invited(string? name, string ownerName, IReadOnlyList<BookingEmailLine> lines, ResponseLinks? respond)
    {
        var subject = lines.Count == 1
            ? $"Invitation: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"Invitation: {lines.Count} bookings in {lines[0].SpaceName}";
        var intro = lines.Count == 1 ? $"{ownerName} has invited you to a booking:" : $"{ownerName} has invited you to {lines.Count} bookings:";
        var paragraphs = new List<string> { Hello(name), Encode(intro), List(lines) };
        if (respond != null) paragraphs.Add(Encode($"Please respond, so {ownerName} knows whether you're coming:"));
        return new EmailContent(subject, Html("You're invited", paragraphs, respond == null
            ? []
            : [new EmailButton("Accept", respond.Accept), new EmailButton("Maybe", respond.Tentative),new EmailButton("Decline", respond.Decline)]));
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

    /* To the people invited when the booking is cancelled, by the owner or anyone else; subject / message as in
     * Cancelled. */
    public static EmailContent InviteCancelled(string? name, string ownerName, IReadOnlyList<BookingEmailLine> lines, string? reason,
        string? subject = null, string? message = null)
    {
        var what = lines.Count == 1 ? "booking you were invited to has" : $"{lines.Count} bookings you were invited to have";
        var paragraphs = new List<string> { Hello(name) };
        paragraphs.AddRange(Note(message));
        paragraphs.Add(Encode($"{ownerName}'s {what} been cancelled" + (reason != null ? $" ({reason})." : ".")));
        paragraphs.Add(List(lines));
        return new EmailContent(CancelSubject(lines, subject),
            Html(lines.Count == 1 ? "Booking cancelled" : "Bookings cancelled", paragraphs));
    }

    /* To the owner when someone they invited answers, or changes their answer: "Accepted: Room 4, Tue 6 Oct", like
     * Teams. A series answered at once is one email listing the dates. */
    public static EmailContent Responded(string name, string attendeeName, AttendeeResponse response, IReadOnlyList<BookingEmailLine> lines,
        string? bookingsUrl)
    {
        /* The subject word, and the sentence "{name} has … your booking" (Maybe in Gmail's words). */
        var (word, verb) = response switch
        {
            AttendeeResponse.Accepted => ("Accepted", "accepted"),
            AttendeeResponse.Tentative => ("Maybe", "said maybe to"),
            AttendeeResponse.Declined => ("Declined", "declined"),
            _ => throw new ArgumentOutOfRangeException(nameof(response), response, null),
        };
        var subject = lines.Count == 1
            ? $"{word}: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{word}: {lines.Count} bookings in {lines[0].SpaceName}";
        var what = lines.Count == 1 ? "your booking" : $"{lines.Count} of your bookings";
        var heading = response == AttendeeResponse.Tentative ? $"{attendeeName} said maybe" : $"{attendeeName} {verb}";
        return new EmailContent(subject, Html(heading,
            [$"Hello {Encode(name)},", Encode($"{attendeeName} has {verb} {what}:"), List(lines)],
            "View my bookings", bookingsUrl));
    }

    /* "Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 10:00–11:00 (UTC+02:00) Warsaw". */
    public static string Describe(BookingEmailLine line)
        => $"{line.SpaceName}{(line.Place.Length > 0 ? " · " + line.Place : "")} — {When(line)}";

    /* The local date and times, then the zone the way Outlook writes it; a booking that runs past midnight shows the
     * end date too. */
    public static string When(BookingEmailLine line)
    {
        var zone = BuildingCalendar.Zone(line.TimeZoneId);
        var start = BuildingCalendar.ToLocal(line.StartUtc, zone);
        var end = BuildingCalendar.ToLocal(line.EndUtc, zone);
        var endText = end.Date == start.Date ? end.ToString("HH:mm", Invariant) : end.ToString("ddd d MMM yyyy, HH:mm", Invariant);
        return $"{start.ToString("ddd d MMM yyyy, HH:mm", Invariant)}–{endText} {ZoneLabel(line.StartUtc, line.TimeZoneId)}";
    }

    /* "(UTC+03:00) Amman": the offset on that date (so summer time shows) and the zone's city. UTC itself is "(UTC)". */
    public static string ZoneLabel(DateTime utc, string timeZoneId)
    {
        var city = BuildingCalendar.ZoneCity(timeZoneId);
        if (city == "UTC") return "(UTC)";
        var offset = BuildingCalendar.Zone(timeZoneId).GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"(UTC{sign}{offset.Duration():hh\\:mm}) {city}";
    }

    private static string Day(BookingEmailLine line)
        => BuildingCalendar.ToLocal(line.StartUtc, BuildingCalendar.Zone(line.TimeZoneId)).ToString("ddd d MMM", Invariant);

    private static string LocalTime(DateTime utc, string timeZoneId)
        => BuildingCalendar.ToLocal(utc, BuildingCalendar.Zone(timeZoneId)).ToString("HH:mm", Invariant);

    private static string CancelSubject(IReadOnlyList<BookingEmailLine> lines, string? custom)
        => !string.IsNullOrWhiteSpace(custom) ? custom.Trim()
            : lines.Count == 1 ? $"Booking cancelled: {lines[0].SpaceName}, {Day(lines[0])}"
            : $"{lines.Count} bookings cancelled";

    /* The canceller's own words, set apart like a quote, line breaks kept. */
    private static IEnumerable<string> Note(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) yield break;
        yield return $"<span style=\"display:block;border-left:3px solid #d0d7de;padding-left:12px\">{EncodeMultiline(message.Trim())}</span>";
    }

    private static string Hello(string? name) => string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {Encode(name)},";

    private static string List(IEnumerable<BookingEmailLine> lines)
        => "<ul style=\"margin:0;padding-left:20px\">"
           + string.Concat(lines.OrderBy(l => l.StartUtc).Select(l => $"<li style=\"margin-bottom:4px\">{Encode(Describe(l))}</li>"))
           + "</ul>";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
}
