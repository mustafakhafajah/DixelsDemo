using System;

namespace Dixels.Portal.Maintenance;

public class ScheduleMaintenanceResultDto
{
    public Guid? SeriesId { get; set; }
    public int Created { get; set; }
    public int AffectedBookingsCount { get; set; }
}
