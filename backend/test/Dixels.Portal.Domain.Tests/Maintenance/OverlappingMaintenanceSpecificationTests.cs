using System;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Maintenance;

public class OverlappingMaintenanceSpecificationTests
{
    private static readonly DateTime Day = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly OverlappingMaintenanceSpecification NineToTen = new(Day.AddHours(9), Day.AddHours(10));

    private static MaintenanceWindow At(double startHour, double endHour)
        => new(Guid.NewGuid(), Guid.NewGuid(), Day.AddHours(startHour), Day.AddHours(endHour));

    [Theory]
    [InlineData(9.5, 10.5)]
    [InlineData(8, 11)]
    [InlineData(9, 10)]
    public void Overlapping_windows_match(double start, double end)
        => NineToTen.IsSatisfiedBy(At(start, end)).ShouldBeTrue();

    [Theory]
    [InlineData(8, 9)]
    [InlineData(10, 11)]
    public void Touching_windows_do_not_match(double start, double end)
        => NineToTen.IsSatisfiedBy(At(start, end)).ShouldBeFalse();

    [Fact]
    public void Cancelled_windows_never_overlap()
    {
        var window = At(9, 10);
        window.Status = MaintenanceStatus.Cancelled;

        NineToTen.IsSatisfiedBy(window).ShouldBeFalse();
    }
}
