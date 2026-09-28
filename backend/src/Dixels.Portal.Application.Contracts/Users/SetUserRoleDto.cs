using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Users;

public class SetUserRoleDto
{
    /* "admin" or "employee". */
    [Required] public string Role { get; set; } = null!;
}
