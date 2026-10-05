using System;
using Dixels.Portal.Common;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Bookings;

/* The email texts are pure, so these need no database or ABP base class. */
public class BookingEmailsTests
{
    /* 08:00–09:00 UTC on Mon 6 Oct 2026 is 10:00–11:00 in Warsaw (summer time). */
    private static readonly BookingEmailLine Room4 = new("Room 4", "HQ North · Floor 3",
        new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc), "Europe/Warsaw");

    [Fact]
    public void Times_are_shown_in_the_building_time_zone() =>
        BookingEmails.Describe(Room4).ShouldBe("Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 10:00–11:00 (Europe/Warsaw)");

    [Fact]
    public void A_booking_past_midnight_shows_the_end_date() =>
        BookingEmails.When(Room4 with { EndUtc = new DateTime(2026, 10, 6, 23, 0, 0, DateTimeKind.Utc) })
            .ShouldBe("Tue 6 Oct 2026, 10:00–Wed 7 Oct 2026, 01:00 (Europe/Warsaw)");

    [Fact]
    public void One_booking_confirmation_names_the_space_and_day()
    {
        var email = BookingEmails.Confirmed("Sara", [Room4], "https://portal.example/app/bookings");

        email.Subject.ShouldBe("Booking confirmed: Room 4, Tue 6 Oct");
        email.Html.ShouldContain("Hello Sara,");
        email.Html.ShouldContain("10:00–11:00");
        email.Html.ShouldContain("https://portal.example/app/bookings");
    }

    [Fact]
    public void A_series_is_one_email_listing_every_date()
    {
        var email = BookingEmails.Confirmed("Sara", [Room4, Room4 with { StartUtc = Room4.StartUtc.AddDays(7), EndUtc = Room4.EndUtc.AddDays(7) }], null);

        email.Subject.ShouldBe("2 bookings confirmed: Room 4");
        email.Html.ShouldContain("Tue 6 Oct 2026");
        email.Html.ShouldContain("Tue 13 Oct 2026");
        email.Html.ShouldNotContain("<a href"); // no portal address configured: no button
    }

    [Fact]
    public void Rescheduled_shows_the_new_and_the_old_time()
    {
        var email = BookingEmails.Rescheduled("Sara", Room4, Room4 with { StartUtc = Room4.StartUtc.AddHours(2), EndUtc = Room4.EndUtc.AddHours(2) }, null);

        email.Subject.ShouldBe("Booking changed: Room 4, Tue 6 Oct");
        email.Html.ShouldContain("Now:</strong> " + PortalEmailLayout.Encode("Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 12:00–13:00"));
        email.Html.ShouldContain("Was: " + PortalEmailLayout.Encode("Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 10:00–11:00"));
    }

    [Fact]
    public void Cancelled_by_an_admin_says_so_and_why()
    {
        var email = BookingEmails.Cancelled("Sara", [Room4], cancelledByOther: true, "the space is blocked at that time", null);

        email.Subject.ShouldBe("Booking cancelled: Room 4, Tue 6 Oct");
        email.Html.ShouldContain("cancelled by an administrator (the space is blocked at that time).");
    }

    [Fact]
    public void Cancelled_by_yourself_does_not_mention_an_admin() =>
        BookingEmails.Cancelled("Sara", [Room4], cancelledByOther: false, null, null).Html.ShouldNotContain("administrator");

    [Fact]
    public void Reminder_gives_the_local_start_time() =>
        BookingEmails.Reminder("Sara", Room4, null).Subject.ShouldBe("Starting soon: Room 4 at 10:00");

    [Fact]
    public void Names_are_html_encoded() =>
        BookingEmails.Confirmed("<b>Sara</b>", [Room4 with { SpaceName = "R&D <lab>" }], null).Html
            .ShouldContain("Hello &lt;b&gt;Sara&lt;/b&gt;,");
}
