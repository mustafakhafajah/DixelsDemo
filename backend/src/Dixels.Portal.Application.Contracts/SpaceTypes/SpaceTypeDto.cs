using System;
using System.Collections.Generic;
using Dixels.Portal.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.SpaceTypes;

public class SpaceTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    /* Every language the record has, English included, for the edit form. Name above is the reader's language,
     * or English when it has none. */
    public List<TranslationDto> Translations { get; set; } = new();
    public int SpaceCount { get; set; }
}
