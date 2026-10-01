using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.SpaceTypes;

public class SpaceTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    /* False when Name (and Note) is shown in a fallback language because none was typed in the reader's. */
    public bool IsTranslated { get; set; }
    public int SpaceCount { get; set; }
}
