using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Estate;

/* Closed days are days in the building's own time zone. Asia/Dubai is UTC+4 all year (no daylight saving). */
public class BuildingCalendarTests
{
    private static ResolvedConstraints Rules(string timeZone, DateOnly[]? holidays = null, int[]? closedWeekdays = null)
        => new(8 * 60, 20 * 60, 15, 8, new List<DateOnly>(holidays ?? []), new List<int>(closedWeekdays ?? []), timeZone);

    private static DateTime Utc(int y, int mo, int d, int h) => new(y, mo, d, h, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly Christmas = new(2026, 12, 25);

    [Fact]
    public void A_holiday_starts_at_the_buildings_local_midnight()
    {
        var dubai = Rules("Asia/Dubai", holidays: [Christmas]);

        /* 24 Dec 21:00 UTC is already 25 Dec 01:00 in Dubai. */
        BuildingCalendar.ClosedReason(dubai, Utc(2026, 12, 24, 21), Utc(2026, 12, 24, 22))
            .ShouldBe("for a holiday on 2026-12-25");
        /* 25 Dec 21:00 UTC is 26 Dec 01:00 in Dubai: the holiday is over there. */
        BuildingCalendar.ClosedReason(dubai, Utc(2026, 12, 25, 21), Utc(2026, 12, 25, 22)).ShouldBeNull();
    }

    [Fact]
    public void A_utc_building_uses_the_utc_date()
    {
        var utc = Rules("UTC", holidays: [Christmas]);

        BuildingCalendar.ClosedReason(utc, Utc(2026, 12, 24, 21), Utc(2026, 12, 24, 22)).ShouldBeNull();
        BuildingCalendar.ClosedReason(utc, Utc(2026, 12, 25, 10), Utc(2026, 12, 25, 11)).ShouldNotBeNull();
    }

    [Fact]
    public void Closed_weekdays_repeat_every_week()
    {
        var weekend = Rules("UTC", closedWeekdays: [(int)DayOfWeek.Friday, (int)DayOfWeek.Saturday]);

        /* 2026-10-01 is a Thursday, 2026-10-02 a Friday, 2026-10-10 a Saturday. */
        BuildingCalendar.ClosedReason(weekend, Utc(2026, 10, 1, 10), Utc(2026, 10, 1, 11)).ShouldBeNull();
        BuildingCalendar.ClosedReason(weekend, Utc(2026, 10, 2, 10), Utc(2026, 10, 2, 11)).ShouldBe("on Fridays");
        BuildingCalendar.ClosedReason(weekend, Utc(2026, 10, 10, 10), Utc(2026, 10, 10, 11)).ShouldBe("on Saturdays");
    }

    [Fact]
    public void A_booking_that_runs_into_a_closed_local_day_is_refused()
    {
        /* Thursday 19:00-21:00 UTC is Thursday 23:00 - Friday 01:00 in Dubai. */
        var dubai = Rules("Asia/Dubai", closedWeekdays: [(int)DayOfWeek.Friday]);

        BuildingCalendar.ClosedReason(dubai, Utc(2026, 10, 1, 19), Utc(2026, 10, 1, 21)).ShouldBe("on Fridays");
        /* Ending exactly at local midnight does not touch Friday. */
        BuildingCalendar.ClosedReason(dubai, Utc(2026, 10, 1, 18), Utc(2026, 10, 1, 20)).ShouldBeNull();
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
