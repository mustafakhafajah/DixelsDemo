using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Bookings;

namespace Dixels.Portal.Maintenance;

/* Block time for a space, floor or building. The note is the reason ("Cleaning", "Renovation", ...). */
public class ScheduleMaintenanceDto : PreviewMaintenanceDto
{
    [StringLength(MaintenanceWindowConsts.MaxNoteLength)]
    public string? Note { get; set; }
    /* Bookings that overlap the blocked time are kept unless the admin ticks this. */
    public bool CancelAffectedBookings { get; set; }
    /* With CancelAffectedBookings: an optional subject and note for the cancellation emails. */
    public CancellationMessageDto? Message { get; set; }
}
