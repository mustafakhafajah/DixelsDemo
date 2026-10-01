using System;
using System.Linq;
using System.Linq.Expressions;
using Dixels.Portal.Buildings;
using Dixels.Portal.Localization;
using Dixels.Portal.SpaceTypes;

namespace Dixels.Portal.Common;

/* The same choice as entity.GetName() (the reader's language, else English), written so the database can
 * sort a page by it. */
public static class LocalizedNameQuery
{
    public static Expression<Func<Building, string?>> BuildingName(string language)
        => b => b.Translations
            .Where(t => t.Language == language || t.Language == PortalLanguages.Default).OrderBy(t => t.Language == language ? 0 : 1)
            .Select(t => t.Name).FirstOrDefault();

    public static Expression<Func<SpaceType, string?>> SpaceTypeName(string language)
        => st => st.Translations
            .Where(t => t.Language == language || t.Language == PortalLanguages.Default).OrderBy(t => t.Language == language ? 0 : 1)
            .Select(t => t.Name).FirstOrDefault();
}
