using System;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Spaces;

public class CreateUpdateSpaceDto
{
    [Required, StringLength(SpaceConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    [Required] public Guid TypeId { get; set; }
    public EstateStatus Status { get; set; }
    [Required] public Guid BuildingId { get; set; }
    [Required] public Guid FloorId { get; set; }
    [Range(0, 999)] public int Capacity { get; set; }
    [StringLength(SpaceConsts.MaxNoteLength)]
    public string? Note { get; set; }
    [Range(0, 23)] public int? OpenHourOverride { get; set; }
    [Range(1, 24)] public int? CloseHourOverride { get; set; }
    [Range(5, 1440)] public int? MinBookingMinutesOverride { get; set; }
    [Range(1, 24)] public int? MaxBookingHoursOverride { get; set; }
}
