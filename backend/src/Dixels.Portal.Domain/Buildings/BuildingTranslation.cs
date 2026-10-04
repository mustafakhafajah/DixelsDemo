using System;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities;

namespace Dixels.Portal.Buildings;

/* A building's name in one language. */
public class BuildingTranslation : Entity, IObjectTranslation
{
    public Guid BuildingId { get; private set; }
    public string Language { get; private set; } = null!;
    public string Name { get; set; } = null!;

    protected BuildingTranslation() { }

    public BuildingTranslation(Guid buildingId, string language, string name)
    {
        BuildingId = buildingId;
        Language = language;
        Name = name;
    }

    public override object[] GetKeys() => [BuildingId, Language];
}
