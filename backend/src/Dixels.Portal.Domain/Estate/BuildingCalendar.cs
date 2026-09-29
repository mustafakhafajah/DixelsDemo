using System;
using System.Linq;

namespace Dixels.Portal.Estate;

/* Closed days (holiday dates and weekly closed days) are days in the building's own time zone, not UTC:
 * a building in Dubai closed on Friday is closed from Thursday 20:00 to Friday 20:00 UTC. */
public static class BuildingCalendar
{
    /* Time zones are IANA names ("Europe/Warsaw"); an unknown one is treated as UTC. */
    public static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static bool IsKnownZone(string id)
    {
        if (string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase)) return true;
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    public static DateOnly LocalDay(DateTime utc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    /* Why the window can't be booked, e.g. "on Fridays" or "for a holiday on 2026-12-25", or null when it can.
     * Checks every local day the window touches: its start day and the day of its last minute. */
    public static string? ClosedReason(ResolvedConstraints c, DateTime startUtc, DateTime endUtc)
    {
        if (c.Holidays.Count == 0 && c.ClosedWeekdays.Count == 0) return null;
        var zone = Zone(c.TimeZone);
        var first = LocalDay(startUtc, zone);
        var last = LocalDay(endUtc > startUtc ? endUtc.AddTicks(-1) : startUtc, zone);
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (c.Holidays.Contains(day)) return $"for a holiday on {day:yyyy-MM-dd}";
            if (c.ClosedWeekdays.Contains((int)day.DayOfWeek)) return $"on {day.DayOfWeek}s";
        }
        return null;
    }

    public static bool IsClosedOn(ResolvedConstraints c, DateOnly localDay)
        => c.Holidays.Contains(localDay) || c.ClosedWeekdays.Any(d => d == (int)localDay.DayOfWeek);
}
