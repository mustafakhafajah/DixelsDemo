using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;

namespace Dixels.Portal.Buildings;

public class CreateUpdateBuildingDto
{
    /* The English name: every record must have one. */
    [Required, StringLength(BuildingConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    /* Optional names in the portal's other languages (never "en"); the full set, so a language left out is removed. */
    public List<TranslationDto> Translations { get; set; } = new();
    [Required, StringLength(BuildingConsts.MaxTimeZoneLength)]
    public string TimeZone { get; set; } = "UTC";
    public bool IsBookable { get; set; } = true;
    [Range(0, 23)] public int OpenHour { get; set; } = BuildingConsts.DefaultOpenHour;
    [Range(1, 24)] public int CloseHour { get; set; } = BuildingConsts.DefaultCloseHour;
    [Range(5, 1440)] public int MinBookingMinutes { get; set; } = BuildingConsts.DefaultMinBookingMinutes;
    [Range(1, 24)] public int MaxBookingHours { get; set; } = BuildingConsts.DefaultMaxBookingHours;
    public List<DateOnly> Holidays { get; set; } = new();
    /* 0 = Sunday … 6 = Saturday. */
    public List<int> ClosedWeekdays { get; set; } = new();
}
