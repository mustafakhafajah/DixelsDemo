using System;

namespace Dixels.Portal.Estate;

/* Bookings and maintenance windows move through the same states; the API sends these strings. */
public static class TimeWindowLifecycle
{
    public static string Get(bool isCancelled, DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        if (isCancelled) return "cancelled";
        if (endUtc <= nowUtc) return "ended";
        if (startUtc <= nowUtc) return "in_progress";
        return "scheduled";
    }
}
