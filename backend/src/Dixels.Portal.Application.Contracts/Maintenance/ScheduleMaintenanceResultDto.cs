using System;

namespace Dixels.Portal.Maintenance;

public class ScheduleMaintenanceResultDto
{
    public Guid? SeriesId { get; set; }
    public int Created { get; set; }
    public int AffectedBookingsCount { get; set; }
    /* How many of those were cancelled (0 unless CancelAffectedBookings was ticked). */
    public int CancelledBookingsCount { get; set; }
}
