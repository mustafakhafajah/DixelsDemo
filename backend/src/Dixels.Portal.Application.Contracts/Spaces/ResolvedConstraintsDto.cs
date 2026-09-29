using System;
using System.Collections.Generic;

namespace Dixels.Portal.Spaces;

public class ResolvedConstraintsDto
{
    public int OpenMinute { get; set; }
    public int CloseMinute { get; set; }
    public int MinBookingMinutes { get; set; }
    public int MaxBookingHours { get; set; }
    public List<DateOnly> Holidays { get; set; } = new();
    public List<int> ClosedWeekdays { get; set; } = new();
    /* The building's time zone: holidays and closed weekdays are days there. */
    public string TimeZone { get; set; } = "UTC";
}
