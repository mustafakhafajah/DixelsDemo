using System;
using System.Linq;
using System.Linq.Expressions;
using Dixels.Portal.Buildings;
using Dixels.Portal.Localization;
using Dixels.Portal.SpaceTypes;

namespace Dixels.Portal.Common;

/* The same choice as entity.GetName() (the reader's language, else the default language, else any), written so
 * the database can sort a page by it: the translations are ordered best-first and the first name is taken. */
public static class LocalizedNameQuery
{
    public static Expression<Func<Building, string?>> BuildingName(string language)
        => b => b.Translations
            .OrderBy(t => t.Language == language ? 0 : t.Language == PortalLanguages.Default ? 1 : 2).ThenBy(t => t.Language)
            .Select(t => t.Name).FirstOrDefault();

    public static Expression<Func<SpaceType, string?>> SpaceTypeName(string language)
        => st => st.Translations
            .OrderBy(t => t.Language == language ? 0 : t.Language == PortalLanguages.Default ? 1 : 2).ThenBy(t => t.Language)
            .Select(t => t.Name).FirstOrDefault();
}
