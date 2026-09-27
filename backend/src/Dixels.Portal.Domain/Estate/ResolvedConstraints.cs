using System;
using System.Collections.Generic;

namespace Dixels.Portal.Estate;

/* The rules that actually apply to one space after Space > Floor > Building resolution. */
public record ResolvedConstraints(
    int OpenMinute,
    int CloseMinute,
    int MinBookingMinutes,
    int MaxBookingHours,
    IReadOnlyList<DateOnly> Holidays);
