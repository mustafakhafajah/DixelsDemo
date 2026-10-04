using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Localization;

namespace Dixels.Portal.Languages;

public class LanguagePreferenceDto
{
    /* A code from PortalLanguages ("en", "ar"); null when the user has never chosen one. */
    [StringLength(PortalLanguages.MaxCodeLength)]
    public string? Language { get; set; }
}
