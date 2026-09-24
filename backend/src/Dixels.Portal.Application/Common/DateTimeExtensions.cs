using System;

namespace Dixels.Portal.Common;

public static class DateTimeExtensions
{
    /* The API works in UTC: a time with no kind is taken as UTC, a local time is converted. */
    public static DateTime AsUtc(this DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };
}
