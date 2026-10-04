using System;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities;

namespace Dixels.Portal.Floors;

/* A floor's name in one language. */
public class FloorTranslation : Entity, IObjectTranslation
{
    public Guid FloorId { get; private set; }
    public string Language { get; private set; } = null!;
    public string Name { get; set; } = null!;

    protected FloorTranslation() { }

    public FloorTranslation(Guid floorId, string language, string name)
    {
        FloorId = floorId;
        Language = language;
        Name = name;
    }

    public override object[] GetKeys() => [FloorId, Language];
}
