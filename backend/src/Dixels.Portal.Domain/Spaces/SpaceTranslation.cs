using System;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities;

namespace Dixels.Portal.Spaces;

/* A space's name and note in one language. */
public class SpaceTranslation : Entity, IObjectTranslation
{
    public Guid SpaceId { get; private set; }
    public string Language { get; private set; } = null!;
    public string Name { get; set; } = null!;
    public string? Note { get; set; }

    protected SpaceTranslation() { }

    public SpaceTranslation(Guid spaceId, string language, string name, string? note)
    {
        SpaceId = spaceId;
        Language = language;
        Name = name;
        Note = note;
    }

    public override object[] GetKeys() => [SpaceId, Language];
}
