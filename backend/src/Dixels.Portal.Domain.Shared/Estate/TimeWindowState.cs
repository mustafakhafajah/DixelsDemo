using System;

namespace Dixels.Portal.Estate;

/* Where a booking or blocked window is in time right now. Worked out from the clock, never stored. */
public enum TimeWindowState
{
    Scheduled,
    InProgress,
    Ended,
    Cancelled,
}

public static class TimeWindowStateExtensions
{
    /* The strings the API and the SPA use. This switch is the only place that knows them: a new state
     * that isn't mapped here fails loudly instead of leaking an unknown string to the client. */
    public static string ToApiValue(this TimeWindowState state) => state switch
    {
        TimeWindowState.Scheduled => "scheduled",
        TimeWindowState.InProgress => "in_progress",
        TimeWindowState.Ended => "ended",
        TimeWindowState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
