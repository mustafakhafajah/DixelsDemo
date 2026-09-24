using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Buildings;

public class CreateUpdateBuildingDto
{
    [Required, StringLength(BuildingConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    [Required, StringLength(BuildingConsts.MaxTimeZoneLength)]
    public string TimeZone { get; set; } = "UTC";
    public bool IsBookable { get; set; } = true;
    [Range(0, 23)] public int OpenHour { get; set; } = BuildingConsts.DefaultOpenHour;
    [Range(1, 24)] public int CloseHour { get; set; } = BuildingConsts.DefaultCloseHour;
    [Range(5, 1440)] public int MinBookingMinutes { get; set; } = BuildingConsts.DefaultMinBookingMinutes;
    [Range(1, 24)] public int MaxBookingHours { get; set; } = BuildingConsts.DefaultMaxBookingHours;
    public List<DateOnly> Holidays { get; set; } = new();
}
