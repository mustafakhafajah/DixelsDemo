using System;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Floors;

public class CreateUpdateFloorDto
{
    [Required] public Guid BuildingId { get; set; }
    [Required, StringLength(FloorConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    public EstateStatus Status { get; set; }
    [Range(0, 23)] public int? OpenHourOverride { get; set; }
    [Range(1, 24)] public int? CloseHourOverride { get; set; }
    [Range(5, 1440)] public int? MinBookingMinutesOverride { get; set; }
    [Range(1, 24)] public int? MaxBookingHoursOverride { get; set; }
}
