using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Floors;

public class Floor : FullAuditedAggregateRoot<Guid>, IMultiLingualObject<FloorTranslation>
{
    public Guid BuildingId { get; set; }
    /* The name ("2", "Ground"), one row per language. */
    public ICollection<FloorTranslation> Translations { get; private set; } = new List<FloorTranslation>();
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }

    protected Floor() { }

    public Floor(Guid id, Guid buildingId, string language, string name) : base(id)
    {
        BuildingId = buildingId;
        SetName(language, name);
    }

    /* In the reader's language, or the fallback when it has none. */
    public string GetName(string? language = null) => this.GetTranslation(language)?.Name ?? "";

    public void SetName(string language, string name)
    {
        var t = this.FindTranslation(language);
        if (t == null) Translations.Add(new FloorTranslation(Id, language, name));
        else t.Name = name;
    }
}
