using System;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Estate;

public class TimeWindowLifecycleTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(1);

    [Theory]
    [InlineData(-1, TimeWindowState.Scheduled)]
    [InlineData(0, TimeWindowState.InProgress)]    // the start instant counts as started
    [InlineData(30, TimeWindowState.InProgress)]
    [InlineData(60, TimeWindowState.Ended)]         // the end instant counts as ended
    public void State_follows_the_clock(int minutesAfterStart, TimeWindowState expected)
        => TimeWindowLifecycle.Get(false, Start, End, Start.AddMinutes(minutesAfterStart)).ShouldBe(expected);

    [Fact]
    public void Cancelled_wins_over_time() => TimeWindowLifecycle.Get(true, Start, End, Start).ShouldBe(TimeWindowState.Cancelled);

    /* The SPA branches on these exact strings, so they must not change by accident. */
    [Theory]
    [InlineData(TimeWindowState.Scheduled, "scheduled")]
    [InlineData(TimeWindowState.InProgress, "in_progress")]
    [InlineData(TimeWindowState.Ended, "ended")]
    [InlineData(TimeWindowState.Cancelled, "cancelled")]
    public void Api_values_stay_the_same(TimeWindowState state, string expected) => state.ToApiValue().ShouldBe(expected);

    [Fact]
    public void Every_state_has_an_api_value()
    {
        foreach (var state in Enum.GetValues<TimeWindowState>())
            state.ToApiValue().ShouldNotBeNullOrWhiteSpace();
    }
}
