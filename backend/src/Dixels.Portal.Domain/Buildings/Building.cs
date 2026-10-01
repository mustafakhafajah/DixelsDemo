using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Buildings;

public class Building : FullAuditedAggregateRoot<Guid>, IMultiLingualObject<BuildingTranslation>
{
    /* The name, one row per language. */
    public ICollection<BuildingTranslation> Translations { get; private set; } = new List<BuildingTranslation>();
    public string TimeZone { get; set; } = "UTC";
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public int OpenHour { get; set; } = BuildingConsts.DefaultOpenHour;
    public int CloseHour { get; set; } = BuildingConsts.DefaultCloseHour;
    public int MinBookingMinutes { get; set; } = BuildingConsts.DefaultMinBookingMinutes;
    public int MaxBookingHours { get; set; } = BuildingConsts.DefaultMaxBookingHours;
    /* One-off closed dates, in the building's own time zone. */
    public List<DateOnly> Holidays { get; set; } = new();
    /* Closed every week on these days (0 = Sunday … 6 = Saturday), in the building's own time zone. */
    public List<int> ClosedWeekdays { get; set; } = new();

    protected Building() { }

    public Building(Guid id, string language, string name) : base(id)
    {
        SetName(language, name);
    }

    /* In the reader's language, or the fallback when it has none. */
    public string GetName(string? language = null) => this.GetTranslation(language)?.Name ?? "";

    public void SetName(string language, string name)
    {
        var t = this.FindTranslation(language);
        if (t == null) Translations.Add(new BuildingTranslation(Id, language, name));
        else t.Name = name;
    }
}
