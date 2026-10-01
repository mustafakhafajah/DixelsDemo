using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Buildings;

public class BuildingDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    /* Every language the record has, English included, for the edit form. Name above is the reader's language,
     * or English when it has none. */
    public List<TranslationDto> Translations { get; set; } = new();
    public string TimeZone { get; set; } = null!;
    public bool IsBookable { get; set; } = true;
    public int OpenHour { get; set; }
    public int CloseHour { get; set; }
    public int MinBookingMinutes { get; set; }
    public int MaxBookingHours { get; set; }
    public List<DateOnly> Holidays { get; set; } = new();
    public List<int> ClosedWeekdays { get; set; } = new();
    public int FloorCount { get; set; }
    public int SpaceCount { get; set; }
}
