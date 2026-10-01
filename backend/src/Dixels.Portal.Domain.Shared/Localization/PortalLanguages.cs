using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dixels.Portal.Localization;

/* The languages the portal speaks (ABP's own pages, such as sign-in, keep ABP's language list). To add one:
 * add it here, add Localization/Portal/{code}.json, and add the same code to the SPA's src/i18n/languages.ts
 * with its src/locales/{code}.json.
 * Names typed in any language are stored per language, so no database change is needed. */
public static class PortalLanguages
{
    /* What a name falls back to when it has no translation in the reader's language. */
    public const string Default = "en";

    public const int MaxCodeLength = 10;

    public static readonly IReadOnlyList<(string Code, string DisplayName)> All = new[]
    {
        ("en", "English"),
        ("ar", "العربية"),
    };

    public static bool IsSupported(string? code) => All.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /* The language of the current request (ABP sets the culture from Accept-Language, the user's setting, ...). */
    public static string Current => Normalize(CultureInfo.CurrentUICulture.Name);

    /* "ar-SA" → "ar", "EN" → "en"; anything unsupported → the default. */
    public static string Normalize(string? culture)
    {
        var c = culture?.Trim();
        while (!string.IsNullOrEmpty(c))
        {
            var match = All.FirstOrDefault(l => string.Equals(l.Code, c, StringComparison.OrdinalIgnoreCase));
            if (match.Code != null) return match.Code;
            var dash = c.LastIndexOf('-');
            c = dash > 0 ? c[..dash] : null;
        }
        return Default;
    }
}
