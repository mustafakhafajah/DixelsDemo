using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Floors;

public class FloorDto : EntityDto<Guid>
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = null!;
    public string Name { get; set; } = null!;
    /* Every language the record has, English included, for the edit form. Name above is the reader's language,
     * or English when it has none. */
    public List<TranslationDto> Translations { get; set; } = new();
    public bool IsBookable { get; set; } = true;
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }
    public int SpaceCount { get; set; }
}
