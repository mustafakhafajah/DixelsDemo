using System;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Estate;

public class TimeWindowLifecycleTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(1);

    [Theory]
    [InlineData(-1, "scheduled")]
    [InlineData(0, "in_progress")]    // the start instant counts as started
    [InlineData(30, "in_progress")]
    [InlineData(60, "ended")]         // the end instant counts as ended
    public void State_follows_the_clock(int minutesAfterStart, string expected)
        => TimeWindowLifecycle.Get(false, Start, End, Start.AddMinutes(minutesAfterStart)).ShouldBe(expected);

    [Fact]
    public void Cancelled_wins_over_time() => TimeWindowLifecycle.Get(true, Start, End, Start).ShouldBe("cancelled");
}
