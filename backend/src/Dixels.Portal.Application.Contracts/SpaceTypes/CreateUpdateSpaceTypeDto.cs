using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.SpaceTypes;

public class CreateUpdateSpaceTypeDto
{
    [Required, StringLength(SpaceTypeConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
}
