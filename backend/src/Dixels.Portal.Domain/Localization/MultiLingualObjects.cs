using System.Collections.Generic;
using System.Linq;

namespace Dixels.Portal.Localization;

/* ABP's "multi-lingual entities" pattern (its Volo.Abp.MultiLingualObjects package is not published on NuGet,
 * so the small part we need lives here): an entity keeps its words in one row per language. */
public interface IMultiLingualObject<TTranslation>
    where TTranslation : class, IObjectTranslation
{
    ICollection<TTranslation> Translations { get; }
}

public interface IObjectTranslation
{
    string Language { get; }
}

public static class MultiLingualObjectExtensions
{
    public static TTranslation? FindTranslation<TTranslation>(this IMultiLingualObject<TTranslation> obj, string language)
        where TTranslation : class, IObjectTranslation
        => obj.Translations.FirstOrDefault(t => t.Language == language);

    /* The reader's language, else the default language, else whichever exists, so a name is never blank. */
    public static TTranslation? GetTranslation<TTranslation>(this IMultiLingualObject<TTranslation> obj, string? language = null)
        where TTranslation : class, IObjectTranslation
        => obj.FindTranslation(PortalLanguages.Normalize(language ?? PortalLanguages.Current))
           ?? obj.FindTranslation(PortalLanguages.Default)
           ?? obj.Translations.OrderBy(t => t.Language).FirstOrDefault();

    /* False when what the reader sees is a fallback from another language. */
    public static bool IsTranslated<TTranslation>(this IMultiLingualObject<TTranslation> obj, string? language = null)
        where TTranslation : class, IObjectTranslation
        => obj.FindTranslation(PortalLanguages.Normalize(language ?? PortalLanguages.Current)) != null;
}
