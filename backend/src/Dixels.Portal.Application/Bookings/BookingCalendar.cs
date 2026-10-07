using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dixels.Portal.Bookings;

/* Someone in a calendar invitation: a name when we know one, and an email address. */
public record CalendarPerson(string? Name, string Email);

public record CalendarAttendee(string? Name, string Email, AttendeeResponse Response);

/* One booking as a calendar event. Sequence: the booking's Version, which goes up with every new time and with the
 * cancellation, so mail programs know which copy is the latest. */
public record CalendarEvent(Guid BookingId, int Sequence, DateTime StartUtc, DateTime EndUtc, string Summary, string Location,
    string? Description, CalendarPerson Organizer, IReadOnlyList<CalendarAttendee> Attendees);

/* The calendar part of a booking email (iCalendar, RFC 5545, sent as an RFC 5546 REQUEST or CANCEL), so Outlook,
 * Gmail and phones put the booking in the reader's calendar and keep it up to date. Written by hand because it is
 * a small, fixed format: times always in UTC (no time-zone blocks needed), text escaped and long lines folded as the
 * standard says. A repeating series is one calendar with an event per date. Pure, so it is tested on its own. */
public static class BookingCalendar
{
    public const string Request = "REQUEST";
    public const string Cancel = "CANCEL";

    /* Every booking has one stable UID, so an update or a cancellation replaces the event already in the calendar. */
    public static string Uid(Guid bookingId) => $"booking-{bookingId}@dixels.io";

    public static string Build(string method, IEnumerable<CalendarEvent> events, DateTime stampUtc)
    {
        if (method != Request && method != Cancel) throw new ArgumentOutOfRangeException(nameof(method), method, null);
        var sb = new StringBuilder();
        Line(sb, "BEGIN:VCALENDAR");
        Line(sb, "PRODID:-//Dixels//Portal//EN");
        Line(sb, "VERSION:2.0");
        Line(sb, "CALSCALE:GREGORIAN");
        Line(sb, "METHOD:" + method);
        foreach (var e in events.OrderBy(e => e.StartUtc))
        {
            Line(sb, "BEGIN:VEVENT");
            Line(sb, "UID:" + Uid(e.BookingId));
            Line(sb, "SEQUENCE:" + e.Sequence.ToString(CultureInfo.InvariantCulture));
            Line(sb, "DTSTAMP:" + Utc(stampUtc));
            Line(sb, "DTSTART:" + Utc(e.StartUtc));
            Line(sb, "DTEND:" + Utc(e.EndUtc));
            Line(sb, "SUMMARY:" + Text(e.Summary));
            if (!string.IsNullOrWhiteSpace(e.Location)) Line(sb, "LOCATION:" + Text(e.Location));
            if (!string.IsNullOrWhiteSpace(e.Description)) Line(sb, "DESCRIPTION:" + Text(e.Description));
            Line(sb, "ORGANIZER" + Name(e.Organizer.Name) + ":mailto:" + e.Organizer.Email);
            foreach (var a in e.Attendees)
            {
                Line(sb, "ATTENDEE" + Name(a.Name) + ";CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=" + PartStat(a.Response)
                         + (method == Request ? ";RSVP=TRUE" : "") + ":mailto:" + a.Email);
            }
            Line(sb, "STATUS:" + (method == Cancel ? "CANCELLED" : "CONFIRMED"));
            Line(sb, "TRANSP:OPAQUE");
            Line(sb, "END:VEVENT");
        }
        Line(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /* "20261006T080000Z". */
    public static string Utc(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /* A TEXT value: backslash, semicolon and comma escaped, line breaks written as \n. */
    public static string Text(string value)
        => value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
            .Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    public static string PartStat(AttendeeResponse response) => response switch
    {
        AttendeeResponse.Accepted => "ACCEPTED",
        AttendeeResponse.Tentative => "TENTATIVE",
        AttendeeResponse.Declined => "DECLINED",
        _ => "NEEDS-ACTION",
    };

    /* ";CN=Sara Ali", quoted when the name holds a character that would end the parameter. A parameter can't hold
     * a double quote or a line break at all, so those are dropped. */
    private static string Name(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var clean = new string(name.Where(c => c != '"' && !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0) return "";
        return clean.IndexOfAny([':', ';', ',']) >= 0 ? $";CN=\"{clean}\"" : ";CN=" + clean;
    }

    /* Ends the line with CRLF, folding it first: no line is longer than 75 octets (UTF-8 bytes), and each
     * continuation starts with one space. A character is never split across lines. */
    private static void Line(StringBuilder sb, string line)
    {
        const int limit = 75;
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                sb.Append("\r\n ");
                octets = 1;
            }
            sb.Append(rune.ToString());
            octets += size;
        }
        sb.Append("\r\n");
    }
}
