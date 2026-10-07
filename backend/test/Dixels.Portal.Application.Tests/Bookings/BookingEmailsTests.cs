using System;
using Dixels.Portal.Common;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Bookings;

/* The email texts are pure, so these need no database or ABP base class. */
public class BookingEmailsTests
{
    /* 08:00–09:00 UTC on Tue 6 Oct 2026 is 10:00–11:00 in Warsaw (summer time). */
    private static readonly BookingEmailLine Room4 = new("Room 4", "HQ North · Floor 3",
        new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc), "Europe/Warsaw");

    private static readonly Guid BookingId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Times_are_shown_in_the_given_zone_with_its_offset_and_city() =>
        BookingEmails.Describe(Room4).ShouldBe("Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 10:00–11:00 (UTC+02:00) Warsaw");

    [Fact]
    public void A_reader_in_Amman_sees_their_own_time() =>
        BookingEmails.When(Room4 with { TimeZoneId = "Asia/Amman" }).ShouldBe("Tue 6 Oct 2026, 11:00–12:00 (UTC+03:00) Amman");

    [Fact]
    public void The_offset_follows_summer_time_and_utc_is_plain()
    {
        BookingEmails.ZoneLabel(new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc), "Europe/Warsaw").ShouldBe("(UTC+01:00) Warsaw");
        BookingEmails.ZoneLabel(Room4.StartUtc, "America/New_York").ShouldBe("(UTC-04:00) New York");
        BookingEmails.ZoneLabel(Room4.StartUtc, "UTC").ShouldBe("(UTC)");
    }

    [Fact]
    public void A_booking_past_midnight_shows_the_end_date() =>
        BookingEmails.When(Room4 with { EndUtc = new DateTime(2026, 10, 6, 23, 0, 0, DateTimeKind.Utc) })
            .ShouldBe("Tue 6 Oct 2026, 10:00–Wed 7 Oct 2026, 01:00 (UTC+02:00) Warsaw");

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
    public void The_plain_text_version_has_the_same_content()
    {
        var text = BookingEmails.Confirmed("Sara", [Room4], "https://portal.example/app/bookings").Text;

        text.ShouldContain("Hello Sara,");
        text.ShouldContain("- Room 4 · HQ North · Floor 3 — Tue 6 Oct 2026, 10:00–11:00 (UTC+02:00) Warsaw");
        text.ShouldContain("View my bookings: https://portal.example/app/bookings");
        text.ShouldNotContain("<");
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
    public void A_cancellation_message_replaces_the_subject_and_comes_before_the_list()
    {
        var email = BookingEmails.Cancelled("Sara", [Room4], cancelledByOther: true, null, null,
            subject: "  Office closed Tuesday ", message: "The heating is broken.\nSorry <all>!");

        email.Subject.ShouldBe("Office closed Tuesday");
        email.Html.ShouldContain("The heating is broken.<br>Sorry &lt;all&gt;!");
        email.Html.IndexOf("The heating is broken", StringComparison.Ordinal)
            .ShouldBeLessThan(email.Html.IndexOf(PortalEmailLayout.Encode("Room 4 · HQ North"), StringComparison.Ordinal));
    }

    [Fact]
    public void Invited_people_get_the_same_cancellation_message()
    {
        var email = BookingEmails.InviteCancelled("Abed", "Sara Ali", [Room4], null, subject: "Office closed", message: "Heating broken");

        email.Subject.ShouldBe("Office closed");
        email.Html.ShouldContain("Heating broken");
        email.Html.ShouldContain("Sara Ali&#39;s booking you were invited to has been cancelled.");
    }

    [Fact]
    public void An_empty_cancellation_message_changes_nothing() =>
        BookingEmails.Cancelled("Sara", [Room4], false, null, null, subject: " ", message: "").Subject
            .ShouldBe("Booking cancelled: Room 4, Tue 6 Oct");

    [Fact]
    public void Reminder_gives_the_local_start_time() =>
        BookingEmails.Reminder("Sara", Room4, null).Subject.ShouldBe("Starting soon: Room 4 at 10:00");

    [Fact]
    public void A_portal_user_is_invited_with_accept_tentative_and_decline()
    {
        var email = BookingEmails.Invited("Abed", "Sara Ali", [Room4], ResponseLinks.For("https://portal.example/app/bookings", BookingId));

        email.Subject.ShouldBe("Invitation: Room 4, Tue 6 Oct");
        email.Html.ShouldContain("Hello Abed,");
        email.Html.ShouldContain("Sara Ali has invited you to a booking:");
        email.Html.ShouldContain(PortalEmailLayout.Encode("Please respond, so Sara Ali knows whether you're coming:"));
        email.Html.ShouldNotContain("down as attending");
        foreach (var (button, respond) in new[] { ("Accept", "accept"), ("Tentative", "tentative"), ("Decline", "decline") })
        {
            email.Html.ShouldContain($">{button}</a>");
            email.Html.ShouldContain(PortalEmailLayout.Encode($"https://portal.example/app/bookings?booking={BookingId}&respond={respond}"));
        }
        /* The first button is the filled one, the others outlined. */
        email.Html.IndexOf("background:#1f6feb", StringComparison.Ordinal)
            .ShouldBeLessThan(email.Html.IndexOf(">Accept</a>", StringComparison.Ordinal));
        email.Html.IndexOf(">Accept</a>", StringComparison.Ordinal)
            .ShouldBeLessThan(email.Html.IndexOf("background:#ffffff;color:#1f2328", StringComparison.Ordinal));
    }

    [Fact]
    public void An_outside_guest_gets_the_details_without_buttons()
    {
        var email = BookingEmails.Invited(null, "Sara Ali", [Room4], null);

        email.Html.ShouldContain("Hello,");
        email.Html.ShouldNotContain("<a href");
        email.Html.ShouldNotContain("Decline");
        email.Html.ShouldNotContain("Please respond");
    }

    [Fact]
    public void Taken_off_a_booking_says_who_did_it() =>
        BookingEmails.Uninvited("Abed", "Sara Ali", [Room4]).Html.ShouldContain("Sara Ali has taken you off this booking:");

    [Theory]
    [InlineData(AttendeeResponse.Accepted, "Accepted: Room 4, Tue 6 Oct", "Abed Karim has accepted your booking:")]
    [InlineData(AttendeeResponse.Tentative, "Tentative: Room 4, Tue 6 Oct", "Abed Karim has tentatively accepted your booking:")]
    [InlineData(AttendeeResponse.Declined, "Declined: Room 4, Tue 6 Oct", "Abed Karim has declined your booking:")]
    public void The_owner_hears_each_answer_like_in_teams(AttendeeResponse response, string subject, string sentence)
    {
        var email = BookingEmails.Responded("Sara", "Abed Karim", response, [Room4], null);

        email.Subject.ShouldBe(subject);
        email.Html.ShouldContain("Hello Sara,");
        email.Html.ShouldContain(sentence);
    }

    [Fact]
    public void An_answer_for_a_series_is_one_email_listing_the_dates()
    {
        var email = BookingEmails.Responded("Sara", "Abed Karim", AttendeeResponse.Declined,
            [Room4, Room4 with { StartUtc = Room4.StartUtc.AddDays(7), EndUtc = Room4.EndUtc.AddDays(7) }], null);

        email.Subject.ShouldBe("Declined: 2 bookings in Room 4");
        email.Html.ShouldContain("Tue 13 Oct 2026");
    }

    [Fact]
    public void A_cancelled_invitation_names_the_owner_and_the_reason() =>
        BookingEmails.InviteCancelled(null, "Sara Ali", [Room4], "the space is blocked at that time").Html
            .ShouldContain("Sara Ali&#39;s booking you were invited to has been cancelled (the space is blocked at that time).");

    [Fact]
    public void Names_are_html_encoded() =>
        BookingEmails.Confirmed("<b>Sara</b>", [Room4 with { SpaceName = "R&D <lab>" }], null).Html
            .ShouldContain("Hello &lt;b&gt;Sara&lt;/b&gt;,");
}
