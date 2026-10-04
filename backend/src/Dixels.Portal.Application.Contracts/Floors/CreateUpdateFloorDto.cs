using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;

namespace Dixels.Portal.Floors;

public class CreateUpdateFloorDto
{
    [Required] public Guid BuildingId { get; set; }
    /* The English name: every record must have one. */
    [Required, StringLength(FloorConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    /* Optional names in the portal's other languages (never "en"); the full set, so a language left out is removed. */
    public List<TranslationDto> Translations { get; set; } = new();
    public bool IsBookable { get; set; } = true;
    [Range(0, 23)] public int? OpenHourOverride { get; set; }
    [Range(1, 24)] public int? CloseHourOverride { get; set; }
    [Range(5, 1440)] public int? MinBookingMinutesOverride { get; set; }
    [Range(1, 24)] public int? MaxBookingHoursOverride { get; set; }
}
