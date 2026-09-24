using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Maintenance;

public class ScheduleMaintenanceDto : PreviewMaintenanceDto
{
    [StringLength(MaintenanceWindowConsts.MaxNoteLength)]
    public string? Note { get; set; }
}
