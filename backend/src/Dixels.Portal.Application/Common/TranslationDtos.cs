using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Portal.Localization;

namespace Dixels.Portal.Common;

public static class TranslationDtos
{
    /* What the form sent, for the domain rules to check. */
    public static List<NameTranslation> ToNameTranslations(this IEnumerable<TranslationDto>? input)
        => (input ?? []).Select(t => new NameTranslation(t.Language, t.Name, t.Note)).ToList();

    /* Every language a record has, English first, then the rest by code. */
    public static List<TranslationDto> Of<T>(IEnumerable<T> rows, Func<T, TranslationDto> map) where T : IObjectTranslation
        => rows.OrderBy(t => t.Language != PortalLanguages.Default).ThenBy(t => t.Language).Select(map).ToList();
}
