using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Users;

public class SetUserRoleDto
{
    /* The name of an existing role, ignoring case. */
    [Required] public string Role { get; set; } = null!;
}
