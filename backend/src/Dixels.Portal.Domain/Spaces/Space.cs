using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Spaces;

/* No time zone of its own: a space is always in its building's time zone. */
public class Space : FullAuditedAggregateRoot<Guid>, IMultiLingualObject<SpaceTranslation>
{
    /* The name and note, one row per language. */
    public ICollection<SpaceTranslation> Translations { get; private set; } = new List<SpaceTranslation>();
    public Guid TypeId { get; set; }
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public int Capacity { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }

    protected Space() { }

    /* name and note are the English ones; other languages are added with SetText. */
    public Space(Guid id, string name, Guid buildingId, Guid floorId, Guid typeId, string? note = null) : base(id)
    {
        SetText(PortalLanguages.Default, name, note);
        BuildingId = buildingId;
        FloorId = floorId;
        TypeId = typeId;
    }

    /* In the reader's language, or English when it has none. */
    public string GetName(string? language = null) => this.GetTranslation(language)?.Name ?? "";

    public string? GetNote(string? language = null) => this.GetTranslation(language)?.Note;

    public void SetText(string language, string name, string? note)
    {
        var t = this.FindTranslation(language);
        if (t == null) Translations.Add(new SpaceTranslation(Id, language, name, note));
        else (t.Name, t.Note) = (name, note);
    }
}
