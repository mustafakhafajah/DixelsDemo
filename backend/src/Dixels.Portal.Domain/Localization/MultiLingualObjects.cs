using System.Collections.Generic;
using System.Linq;

namespace Dixels.Portal.Localization;

/* ABP's "multi-lingual entities" pattern (its Volo.Abp.MultiLingualObjects package is not published on NuGet,
 * so the small part we need lives here): an entity keeps its words in one row per language. The English row
 * (PortalLanguages.Default) is required; the other languages are optional extras. */
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

    /* The reader's language, else English. Never another language: an English reader only ever sees English. */
    public static TTranslation? GetTranslation<TTranslation>(this IMultiLingualObject<TTranslation> obj, string? language = null)
        where TTranslation : class, IObjectTranslation
        => obj.FindTranslation(PortalLanguages.Normalize(language ?? PortalLanguages.Current))
           ?? obj.FindTranslation(PortalLanguages.Default);

    /* Drops the extra languages that are not in keep; the English row always stays. */
    public static void RemoveTranslationsExcept<TTranslation>(this IMultiLingualObject<TTranslation> obj, IEnumerable<string> keep)
        where TTranslation : class, IObjectTranslation
    {
        var kept = keep.ToHashSet();
        foreach (var t in obj.Translations.Where(t => t.Language != PortalLanguages.Default && !kept.Contains(t.Language)).ToList())
            obj.Translations.Remove(t);
    }
}

/* One language's words as the admin typed them, before the rules check them. Note is only used by spaces. */
public record NameTranslation(string Language, string Name, string? Note = null);
