using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Localization;

/* A record's words in one language: "ar" → "غرفة الاجتماعات 4". Note is only used by spaces. */
public class TranslationDto
{
    [Required, StringLength(PortalLanguages.MaxCodeLength)]
    public string Language { get; set; } = null!;
    /* Checked by TranslationRules, so a blank one is reported against its language. */
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
}
