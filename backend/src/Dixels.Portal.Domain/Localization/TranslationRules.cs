using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using Volo.Abp;

namespace Dixels.Portal.Localization;

/* The rules for a record's extra languages, shared by buildings, floors, space types and spaces: only the
 * portal's other languages (English has its own required field), each once, with a name that fits.
 * Errors point at "translations.{code}" (or "translations.{code}.note") so the form can show them in place.
 * A name's letters are checked by the manager with its other name rules; a note's here. */
public static class TranslationRules
{
    public static List<NameTranslation> Clean(IStringLocalizer l, IEnumerable<NameTranslation>? input, int maxNameLength,
        int maxNoteLength = 0)
    {
        var cleaned = new List<NameTranslation>();
        foreach (var t in input ?? [])
        {
            var code = t.Language?.Trim();
            if (!PortalLanguages.IsSupported(code) || PortalLanguages.Normalize(code) == PortalLanguages.Default)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.UnsupportedLanguage, message: l["Error:UnsupportedLanguage"])
                    .ForField("translations");
            var language = PortalLanguages.Normalize(code);
            var field = $"translations.{language}";
            var languageName = PortalLanguages.DisplayName(language);
            if (cleaned.Any(c => c.Language == language))
                throw new UserFriendlyException(code: PortalDomainErrorCodes.UnsupportedLanguage, message:
                    l["Error:TranslationTwice", languageName]).ForField(field);

            var name = t.Name?.Trim() ?? "";
            if (name.Length == 0)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message:
                    l["Error:TranslationNameMissing", languageName]).ForField(field);
            if (name.Length > maxNameLength)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.TooLong, message:
                    l["Error:NameTooLong", maxNameLength]).ForField(field);

            var note = maxNoteLength > 0 && !string.IsNullOrWhiteSpace(t.Note) ? t.Note.Trim() : null;
            if (note?.Length > maxNoteLength)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.TooLong, message:
                    l["Error:NoteTooLong", maxNoteLength]).ForField($"{field}.note");
            ScriptRules.EnsureFits(l, language, note, $"{field}.note");
            cleaned.Add(new NameTranslation(language, name, note));
        }
        return cleaned;
    }
}
