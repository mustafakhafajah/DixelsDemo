using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Bookings;

/* The calendar invitations are pure text, so these need no database. */
public class BookingCalendarTests
{
    private static readonly Guid BookingId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTime Stamp = new(2026, 10, 1, 7, 5, 9, DateTimeKind.Utc);

    private static readonly CalendarEvent Room4 = new(BookingId, 3,
        new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 9, 30, 0, DateTimeKind.Utc),
        "Room 4", "Room 4 · HQ North · Floor 3", "https://portal.example/app/bookings?booking=1",
        new CalendarPerson("Sara Ali", "sara@dixels.test"),
        [
            new CalendarAttendee("Abed Karim", "abed@dixels.test", AttendeeResponse.None),
            new CalendarAttendee("Mona", "mona@dixels.test", AttendeeResponse.Accepted),
            new CalendarAttendee(null, "guest@outside.test", AttendeeResponse.Declined),
        ]);

    [Fact]
    public void A_request_has_the_calendar_and_event_basics_in_utc()
    {
        var ics = BookingCalendar.Build(BookingCalendar.Request, [Room4], Stamp);
        var lines = Unfold(ics);

        lines[0].ShouldBe("BEGIN:VCALENDAR");
        lines.ShouldContain("VERSION:2.0");
        lines.ShouldContain("PRODID:-//Dixels//Portal//EN");
        lines.ShouldContain("CALSCALE:GREGORIAN");
        lines.ShouldContain("METHOD:REQUEST");
        lines.ShouldContain($"UID:booking-{BookingId}@dixels.io");
        lines.ShouldContain("SEQUENCE:3");
        lines.ShouldContain("DTSTAMP:20261001T070509Z");
        lines.ShouldContain("DTSTART:20261006T080000Z");
        lines.ShouldContain("DTEND:20261006T093000Z");
        lines.ShouldContain("SUMMARY:Room 4");
        lines.ShouldContain("STATUS:CONFIRMED");
        lines.ShouldContain("ORGANIZER;CN=Sara Ali:mailto:sara@dixels.test");
        lines.Last().ShouldBe("END:VCALENDAR");
        ics.ShouldEndWith("END:VCALENDAR\r\n");
    }

    [Fact]
    public void Each_attendee_has_their_answer_and_is_asked_to_reply()
    {
        var lines = Unfold(BookingCalendar.Build(BookingCalendar.Request, [Room4], Stamp));

        lines.ShouldContain("ATTENDEE;CN=Abed Karim;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:abed@dixels.test");
        lines.ShouldContain("ATTENDEE;CN=Mona;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;RSVP=TRUE:mailto:mona@dixels.test");
        lines.ShouldContain("ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=DECLINED;RSVP=TRUE:mailto:guest@outside.test");
    }

    [Fact]
    public void A_cancellation_is_method_cancel_and_status_cancelled()
    {
        var lines = Unfold(BookingCalendar.Build(BookingCalendar.Cancel, [Room4 with { Sequence = 4 }], Stamp));

        lines.ShouldContain("METHOD:CANCEL");
        lines.ShouldContain("STATUS:CANCELLED");
        lines.ShouldContain("SEQUENCE:4");
        lines.ShouldContain($"UID:booking-{BookingId}@dixels.io");
        lines.ShouldNotContain(l => l.Contains("RSVP"));
    }

    [Fact]
    public void A_series_is_one_calendar_with_an_event_per_date()
    {
        var next = Room4 with { BookingId = Guid.NewGuid(), StartUtc = Room4.StartUtc.AddDays(7), EndUtc = Room4.EndUtc.AddDays(7) };
        var lines = Unfold(BookingCalendar.Build(BookingCalendar.Request, [next, Room4], Stamp));

        lines.Count(l => l == "BEGIN:VEVENT").ShouldBe(2);
        lines.Count(l => l == "BEGIN:VCALENDAR").ShouldBe(1);
        lines.IndexOf("DTSTART:20261006T080000Z").ShouldBeLessThan(lines.IndexOf("DTSTART:20261013T080000Z"));
    }

    [Fact]
    public void Text_is_escaped()
    {
        BookingCalendar.Text("a\\b;c,d\ne\r\nf").ShouldBe("a\\\\b\\;c\\,d\\ne\\nf");

        var lines = Unfold(BookingCalendar.Build(BookingCalendar.Request, [Room4 with { Summary = "R&D; lab, east", Location = "Line 1\nLine 2" }], Stamp));
        lines.ShouldContain("SUMMARY:R&D\\; lab\\, east");
        lines.ShouldContain("LOCATION:Line 1\\nLine 2");
    }

    [Fact]
    public void A_name_with_a_separator_is_quoted_and_double_quotes_dropped()
    {
        var lines = Unfold(BookingCalendar.Build(BookingCalendar.Request,
            [Room4 with { Organizer = new CalendarPerson("Ali, \"Sara\"", "sara@dixels.test") }], Stamp));

        lines.ShouldContain("ORGANIZER;CN=\"Ali, Sara\":mailto:sara@dixels.test");
    }

    [Fact]
    public void Long_lines_are_folded_at_75_octets_without_splitting_a_character()
    {
        var summary = string.Concat(Enumerable.Repeat("Salle de réunion « Été » ", 8));
        var ics = BookingCalendar.Build(BookingCalendar.Request, [Room4 with { Summary = summary }], Stamp);

        ics.ShouldContain("\r\n ");
        ics.Replace("\r\n", "").ShouldNotContain("\n");
        foreach (var line in ics.Split("\r\n"))
            Encoding.UTF8.GetByteCount(line).ShouldBeLessThanOrEqualTo(75);
        Unfold(ics).ShouldContain("SUMMARY:" + summary);
    }

    [Fact]
    public void The_email_has_text_html_and_calendar_parts_and_an_ics_file()
    {
        var ics = BookingCalendar.Build(BookingCalendar.Request, [Room4], Stamp);
        using var mail = BookingCalendarEmailJob.Build(new BookingCalendarEmailArgs
        {
            To = "abed@dixels.test", Subject = "Invitation: Room 4, Tue 6 Oct", Html = "<p>Hi</p>", Text = "Hi",
            Calendar = ics, Method = BookingCalendar.Request,
        });

        mail.To.ShouldHaveSingleItem().Address.ShouldBe("abed@dixels.test");
        mail.AlternateViews.Select(v => v.ContentType.MediaType).ShouldBe(["text/plain", "text/html", "text/calendar"]);
        var calendar = mail.AlternateViews[2];
        calendar.ContentType.Parameters["method"].ShouldBe("REQUEST");
        calendar.ContentType.CharSet.ShouldBe("utf-8");
        new StreamReader(calendar.ContentStream).ReadToEnd().ShouldBe(ics);
        var file = mail.Attachments.ShouldHaveSingleItem();
        file.ContentType.MediaType.ShouldBe("application/ics");
        file.ContentDisposition!.FileName.ShouldBe("invite.ics");
        file.ContentDisposition.DispositionType.ShouldBe(DispositionTypeNames.Attachment);
    }

    /* The calendar's logical lines: folded continuations joined back on. */
    private static System.Collections.Generic.List<string> Unfold(string ics)
        => ics.Replace("\r\n ", "").Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
}
