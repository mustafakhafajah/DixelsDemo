using System;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Bookings;

/* The specification compiles to an in-memory check too, so the overlap rule is tested without a database. */
public class OverlappingBookingsSpecificationTests
{
    private static readonly DateTime Day = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /* The window every test asks about: 09:00-10:00. */
    private static readonly OverlappingBookingsSpecification NineToTen = new(Day.AddHours(9), Day.AddHours(10));

    private static Booking At(double startHour, double endHour)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day.AddHours(startHour), Day.AddHours(endHour));

    [Theory]
    [InlineData(9.5, 10.5)]   // starts inside
    [InlineData(8.5, 9.5)]    // ends inside
    [InlineData(9.25, 9.75)]  // fully inside
    [InlineData(8, 11)]       // fully covers
    [InlineData(9, 10)]       // exactly the same
    public void Overlapping_bookings_match(double start, double end)
        => NineToTen.IsSatisfiedBy(At(start, end)).ShouldBeTrue();

    [Theory]
    [InlineData(8, 9)]    // ends exactly when the window starts
    [InlineData(10, 11)]  // starts exactly when the window ends
    [InlineData(6, 7)]    // well before
    public void Touching_or_separate_bookings_do_not_match(double start, double end)
        => NineToTen.IsSatisfiedBy(At(start, end)).ShouldBeFalse();

    [Fact]
    public void Cancelled_bookings_never_overlap()
    {
        var booking = At(9, 10);
        booking.Status = BookingStatus.Cancelled;

        NineToTen.IsSatisfiedBy(booking).ShouldBeFalse();
    }
}
