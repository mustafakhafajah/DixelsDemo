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

    /* The first closed local day the window touches (a holiday, or a weekly closed day), or null when it is open.
     * Checks every local day the window touches: its start day and the day of its last minute. */
    public static ClosedDay? FindClosedDay(ResolvedConstraints c, DateTime startUtc, DateTime endUtc)
    {
        if (c.Holidays.Count == 0 && c.ClosedWeekdays.Count == 0) return null;
        var zone = Zone(c.TimeZone);
        var first = LocalDay(startUtc, zone);
        var last = LocalDay(endUtc > startUtc ? endUtc.AddTicks(-1) : startUtc, zone);
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (c.Holidays.Contains(day)) return new ClosedDay(day, IsHoliday: true);
            if (c.ClosedWeekdays.Contains((int)day.DayOfWeek)) return new ClosedDay(day, IsHoliday: false);
        }
        return null;
    }

    public static bool IsClosedOn(ResolvedConstraints c, DateOnly localDay)
        => c.Holidays.Contains(localDay) || c.ClosedWeekdays.Any(d => d == (int)localDay.DayOfWeek);
}

/* A local day a building is closed: a one-off holiday, or one of its weekly closed days. */
public record ClosedDay(DateOnly Day, bool IsHoliday);
