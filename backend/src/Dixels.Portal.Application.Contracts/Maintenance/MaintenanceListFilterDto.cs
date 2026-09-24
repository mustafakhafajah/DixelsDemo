using System;

namespace Dixels.Portal.Maintenance;

public class MaintenanceListFilterDto
{
    public Guid? SpaceId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool IncludeCancelled { get; set; }
}
