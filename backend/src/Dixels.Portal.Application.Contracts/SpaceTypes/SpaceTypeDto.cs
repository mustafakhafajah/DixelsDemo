using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.SpaceTypes;

public class SpaceTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public int SpaceCount { get; set; }
}
