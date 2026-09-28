using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Users;

public class UpdateUserPermissionsDto
{
    [Required] public List<UpdateUserPermissionDto> Permissions { get; set; } = new();
}

public class UpdateUserPermissionDto
{
    [Required] public string Name { get; set; } = null!;
    public bool IsGranted { get; set; }
}
