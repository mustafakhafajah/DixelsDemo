using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Estate;

/* Closed days and opening hours are read in the building's own time zone. Asia/Dubai is UTC+4 and Asia/Riyadh
 * UTC+3 all year (no daylight saving); Europe/London is UTC+0 in winter and UTC+1 in summer. */
public class BuildingCalendarTests
{
    private static ResolvedConstraints Rules(string timeZone, DateOnly[]? holidays = null, int[]? closedWeekdays = null,
        int openHour = 8, int closeHour = 20)
        => new(openHour * 60, closeHour * 60, 15, 8, new List<DateOnly>(holidays ?? []), new List<int>(closedWeekdays ?? []), timeZone);

    private static DateTime Utc(int y, int mo, int d, int h) => new(y, mo, d, h, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly Christmas = new(2026, 12, 25);

    [Fact]
    public void A_holiday_starts_at_the_buildings_local_midnight()
    {
        var dubai = Rules("Asia/Dubai", holidays: [Christmas]);

        /* 24 Dec 21:00 UTC is already 25 Dec 01:00 in Dubai. */
        BuildingCalendar.FindClosedDay(dubai, Utc(2026, 12, 24, 21), Utc(2026, 12, 24, 22))
            .ShouldBe(new ClosedDay(Christmas, IsHoliday: true));
        /* 25 Dec 21:00 UTC is 26 Dec 01:00 in Dubai: the holiday is over there. */
        BuildingCalendar.FindClosedDay(dubai, Utc(2026, 12, 25, 21), Utc(2026, 12, 25, 22)).ShouldBeNull();
    }

    [Fact]
    public void A_utc_building_uses_the_utc_date()
    {
        var utc = Rules("UTC", holidays: [Christmas]);

        BuildingCalendar.FindClosedDay(utc, Utc(2026, 12, 24, 21), Utc(2026, 12, 24, 22)).ShouldBeNull();
        BuildingCalendar.FindClosedDay(utc, Utc(2026, 12, 25, 10), Utc(2026, 12, 25, 11)).ShouldNotBeNull();
    }

    [Fact]
    public void Closed_weekdays_repeat_every_week()
    {
        var weekend = Rules("UTC", closedWeekdays: [(int)DayOfWeek.Friday, (int)DayOfWeek.Saturday]);

        /* 2026-10-01 is a Thursday, 2026-10-02 a Friday, 2026-10-10 a Saturday. */
        BuildingCalendar.FindClosedDay(weekend, Utc(2026, 10, 1, 10), Utc(2026, 10, 1, 11)).ShouldBeNull();
        BuildingCalendar.FindClosedDay(weekend, Utc(2026, 10, 2, 10), Utc(2026, 10, 2, 11)).ShouldBe(new ClosedDay(new DateOnly(2026, 10, 2), IsHoliday: false));
        BuildingCalendar.FindClosedDay(weekend, Utc(2026, 10, 10, 10), Utc(2026, 10, 10, 11)).ShouldBe(new ClosedDay(new DateOnly(2026, 10, 10), IsHoliday: false));
    }

    [Fact]
    public void A_booking_that_runs_into_a_closed_local_day_is_refused()
    {
        /* Thursday 19:00-21:00 UTC is Thursday 23:00 - Friday 01:00 in Dubai. */
        var dubai = Rules("Asia/Dubai", closedWeekdays: [(int)DayOfWeek.Friday]);

        BuildingCalendar.FindClosedDay(dubai, Utc(2026, 10, 1, 19), Utc(2026, 10, 1, 21)).ShouldBe(new ClosedDay(new DateOnly(2026, 10, 2), IsHoliday: false));
        /* Ending exactly at local midnight does not touch Friday. */
        BuildingCalendar.FindClosedDay(dubai, Utc(2026, 10, 1, 18), Utc(2026, 10, 1, 20)).ShouldBeNull();
    }

    [Fact]
    public void Opening_hours_are_the_buildings_local_hours()
    {
        /* Riyadh, open 08:00-18:00 local = 05:00-15:00 UTC. */
        var riyadh = Rules("Asia/Riyadh", openHour: 8, closeHour: 18);

        BuildingCalendar.IsWithinHours(riyadh, Utc(2026, 11, 2, 5), Utc(2026, 11, 2, 6)).ShouldBeTrue();    // 08:00-09:00 local
        BuildingCalendar.IsWithinHours(riyadh, Utc(2026, 11, 2, 14), Utc(2026, 11, 2, 15)).ShouldBeTrue();  // 17:00-18:00 local
        BuildingCalendar.IsWithinHours(riyadh, Utc(2026, 11, 2, 4), Utc(2026, 11, 2, 5)).ShouldBeFalse();   // 07:00 local, before opening
        BuildingCalendar.IsWithinHours(riyadh, Utc(2026, 11, 2, 16), Utc(2026, 11, 2, 17)).ShouldBeFalse(); // 19:00 local, after closing
    }

    [Fact]
    public void Opening_hours_follow_daylight_saving()
    {
        /* London clocks go forward on 29 March 2026: 08:00 local is 08:00 UTC the Friday before, 07:00 UTC the Monday after. */
        var london = Rules("Europe/London", openHour: 8, closeHour: 18);

        BuildingCalendar.IsWithinHours(london, Utc(2026, 3, 27, 8), Utc(2026, 3, 27, 9)).ShouldBeTrue();
        BuildingCalendar.IsWithinHours(london, Utc(2026, 3, 27, 7), Utc(2026, 3, 27, 8)).ShouldBeFalse();
        BuildingCalendar.IsWithinHours(london, Utc(2026, 3, 27, 17), Utc(2026, 3, 27, 18)).ShouldBeTrue();

        BuildingCalendar.IsWithinHours(london, Utc(2026, 3, 30, 7), Utc(2026, 3, 30, 8)).ShouldBeTrue();
        BuildingCalendar.IsWithinHours(london, Utc(2026, 3, 30, 17), Utc(2026, 3, 30, 18)).ShouldBeFalse(); // 18:00-19:00 local
    }

    [Fact]
    public void A_window_may_end_at_local_midnight_but_not_run_past_it()
    {
        /* Tokyo (UTC+9), open all day: 14:00 UTC is 23:00 local. */
        var tokyo = Rules("Asia/Tokyo", openHour: 0, closeHour: 24);

        BuildingCalendar.IsWithinHours(tokyo, Utc(2026, 11, 2, 14), Utc(2026, 11, 2, 15)).ShouldBeTrue();   // 23:00-00:00 local
        BuildingCalendar.IsWithinHours(tokyo, Utc(2026, 11, 2, 14), Utc(2026, 11, 2, 16)).ShouldBeFalse();  // 23:00-01:00 local
        /* 23:00-01:00 UTC is 08:00-10:00 the next local day: crossing UTC midnight is fine. */
        BuildingCalendar.IsWithinHours(tokyo, Utc(2026, 11, 2, 23), Utc(2026, 11, 3, 1)).ShouldBeTrue();
    }

    [Fact]
    public void Only_iana_time_zone_names_are_accepted()
    {
        /* A Windows name the server could resolve on Windows, but the browser cannot. */
        BuildingCalendar.IsKnownZone("Arab Standard Time").ShouldBeFalse();
        BuildingCalendar.IsKnownZone("Asia/Riyadh").ShouldBeTrue();
    }

    [Fact]
    public void An_unknown_time_zone_counts_as_utc()
    {
        BuildingCalendar.IsKnownZone("Mars/Base").ShouldBeFalse();
        BuildingCalendar.IsKnownZone("Europe/Warsaw").ShouldBeTrue();
        BuildingCalendar.IsKnownZone("UTC").ShouldBeTrue();
        BuildingCalendar.Zone("Mars/Base").ShouldBe(TimeZoneInfo.Utc);
    }
}
