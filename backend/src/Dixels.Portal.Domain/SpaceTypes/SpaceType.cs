using System;
using System.Collections.Generic;
using Dixels.Portal.Localization;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.SpaceTypes;

/* A kind of space ("Meeting room", "Desk", ...). Admins manage the list; every space has exactly one type. */
public class SpaceType : AuditedAggregateRoot<Guid>, IMultiLingualObject<SpaceTypeTranslation>
{
    /* The name, one row per language. */
    public ICollection<SpaceTypeTranslation> Translations { get; private set; } = new List<SpaceTypeTranslation>();

    protected SpaceType() { }

    /* name is the English name; other languages are added with SetName. */
    public SpaceType(Guid id, string name) : base(id)
    {
        SetName(PortalLanguages.Default, name);
    }

    /* In the reader's language, or English when it has none. */
    public string GetName(string? language = null) => this.GetTranslation(language)?.Name ?? "";

    public void SetName(string language, string name)
    {
        var t = this.FindTranslation(language);
        if (t == null) Translations.Add(new SpaceTypeTranslation(Id, language, name));
        else t.Name = name;
    }
}
