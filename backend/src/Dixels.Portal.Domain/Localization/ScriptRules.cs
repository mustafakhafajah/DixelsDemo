using Microsoft.Extensions.Localization;
using Volo.Abp;

namespace Dixels.Portal.Localization;

/* Text typed for a language must be written in that language's letters. Only letters count: digits, spaces,
 * punctuation and Arabic diacritics are fine anywhere, and a name with no letters at all ("2") fits every language.
 *   Latin (English):  every letter is Latin.
 *   Arabic:           every letter is Arabic or Latin (codes such as "VIP" may stay), and at least one is Arabic.
 * The SPA's src/i18n/languages.ts checks the same Unicode ranges, so the form and the server agree. */
public static class ScriptRules
{
    public static bool Fits(LanguageScript script, string? text)
    {
        var letters = false;
        var own = false;
        foreach (var c in text ?? "")
        {
            if (!char.IsLetter(c)) continue;
            letters = true;
            if (IsLatin(c)) own |= script == LanguageScript.Latin;
            else if (IsArabic(c) && script == LanguageScript.Arabic) own = true;
            else return false;
        }
        return !letters || own || script == LanguageScript.Latin;
    }

    /* field is where the form shows the message: "name", "note", "translations.ar", "translations.ar.note". */
    public static void EnsureFits(IStringLocalizer l, string language, string? text, string field)
    {
        var script = PortalLanguages.ScriptOf(language);
        if (!Fits(script, text))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.WrongScript, message:
                l[$"Error:WrongScript:{script}", PortalLanguages.DisplayName(language)]).ForField(field);
    }

    private static bool IsLatin(char c)
        => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
           || c is >= 'À' and <= 'ɏ' && c != '×' && c != '÷'
           || c is >= 'Ḁ' and <= 'ỿ';

    private static bool IsArabic(char c)
        => c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ' or >= 'ࡰ' and <= 'ࣿ'
            or >= 'ﭐ' and <= '﷿' or >= 'ﹰ' and <= '﻿';
}
