using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Localization;

namespace Dixels.Portal.SpaceTypes;

public class CreateUpdateSpaceTypeDto
{
    /* The English name: every record must have one. */
    [Required, StringLength(SpaceTypeConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    /* Optional names in the portal's other languages (never "en"); the full set, so a language left out is removed. */
    public List<TranslationDto> Translations { get; set; } = new();
}
