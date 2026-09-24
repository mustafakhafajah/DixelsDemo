using System;
using System.Collections.Generic;
using Dixels.Portal.Buildings;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Estate;

/* ConstraintResolver is pure (no database, no clock), so these are plain unit tests with no ABP base class. */
public class ConstraintResolverTests
{
    private static Building HqNorth() => new(Guid.NewGuid(), "HQ North")
    {
        OpenHour = 8,
        CloseHour = 20,
        MinBookingMinutes = 15,
        MaxBookingHours = 8,
    };

    private static Floor FloorOf(Building b) => new(Guid.NewGuid(), b.Id, "3");

    private static Space SpaceOn(Building b, Floor f) => new(Guid.NewGuid(), "Conference Room A", b.Id, f.Id);

    [Fact]
    public void Everything_inherits_the_building_when_nothing_is_overridden()
    {
        var building = HqNorth();
        var floor = FloorOf(building);
        var space = SpaceOn(building, floor);

        var c = ConstraintResolver.Resolve(building, floor, space);

        c.OpenMinute.ShouldBe(8 * 60);
        c.CloseMinute.ShouldBe(20 * 60);
        c.MinBookingMinutes.ShouldBe(15);
        c.MaxBookingHours.ShouldBe(8);
    }

    [Fact]
    public void Each_field_takes_the_most_specific_value_independently()
    {
        var building = HqNorth();
        var floor = FloorOf(building);
        floor.OpenHourOverride = 9;
        floor.MaxBookingHoursOverride = 4;
        var space = SpaceOn(building, floor);
        space.CloseHourOverride = 18;
        space.MinBookingMinutesOverride = 30;

        var c = ConstraintResolver.Resolve(building, floor, space);

        c.OpenMinute.ShouldBe(9 * 60);       // floor
        c.CloseMinute.ShouldBe(18 * 60);     // space
        c.MinBookingMinutes.ShouldBe(30);    // space
        c.MaxBookingHours.ShouldBe(4);       // floor
    }

    [Fact]
    public void Space_override_beats_floor_override()
    {
        var building = HqNorth();
        var floor = FloorOf(building);
        floor.OpenHourOverride = 9;
        var space = SpaceOn(building, floor);
        space.OpenHourOverride = 10;

        ConstraintResolver.Resolve(building, floor, space).OpenMinute.ShouldBe(10 * 60);
    }

    [Fact]
    public void Missing_floor_falls_back_to_the_building()
    {
        var building = HqNorth();
        var space = SpaceOn(building, FloorOf(building));
        space.MinBookingMinutesOverride = 30;

        var c = ConstraintResolver.Resolve(building, null, space);

        c.OpenMinute.ShouldBe(8 * 60);
        c.MinBookingMinutes.ShouldBe(30);
    }

    [Fact]
    public void Closing_at_24_means_end_of_day()
    {
        var building = HqNorth();
        building.CloseHour = 24;

        ConstraintResolver.Resolve(building, null, SpaceOn(building, FloorOf(building)))
            .CloseMinute.ShouldBe(1440);
    }

    [Fact]
    public void Holidays_come_from_the_building()
    {
        var building = HqNorth();
        var christmas = new DateOnly(2026, 12, 25);
        building.Holidays = new List<DateOnly> { christmas };

        ConstraintResolver.Resolve(building, null, SpaceOn(building, FloorOf(building)))
            .Holidays.ShouldBe(new[] { christmas });
    }

    [Fact]
    public void ResolveBounds_shows_what_a_new_space_on_that_floor_would_inherit()
    {
        var building = HqNorth();
        var floor = FloorOf(building);
        floor.CloseHourOverride = 18;

        var bounds = ConstraintResolver.ResolveBounds(building, floor);

        bounds.OpenHour.ShouldBe(8);
        bounds.CloseHour.ShouldBe(18);
        bounds.MinBookingMinutes.ShouldBe(15);
        bounds.MaxBookingHours.ShouldBe(8);
    }
}
