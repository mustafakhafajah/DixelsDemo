using System;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities;

namespace Dixels.Portal.SpaceTypes;

/* A space type's name in one language. */
public class SpaceTypeTranslation : Entity, IObjectTranslation
{
    public Guid SpaceTypeId { get; private set; }
    public string Language { get; private set; } = null!;
    public string Name { get; set; } = null!;

    protected SpaceTypeTranslation() { }

    public SpaceTypeTranslation(Guid spaceTypeId, string language, string name)
    {
        SpaceTypeId = spaceTypeId;
        Language = language;
        Name = name;
    }

    public override object[] GetKeys() => [SpaceTypeId, Language];
}
