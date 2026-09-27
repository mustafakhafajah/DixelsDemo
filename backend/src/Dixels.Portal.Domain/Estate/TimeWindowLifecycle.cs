using System;

namespace Dixels.Portal.Estate;

/* Bookings and maintenance windows move through the same states. */
public static class TimeWindowLifecycle
{
    public static TimeWindowState Get(bool isCancelled, DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        if (isCancelled) return TimeWindowState.Cancelled;
        if (endUtc <= nowUtc) return TimeWindowState.Ended;
        if (startUtc <= nowUtc) return TimeWindowState.InProgress;
        return TimeWindowState.Scheduled;
    }
}
