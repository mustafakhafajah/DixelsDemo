namespace Dixels.Portal.Users;

public class UserDirectoryRoleDto
{
    /* The role's name, as sent back in Role / Roles and accepted by create, set role and the filter. */
    public string Name { get; set; } = null!;
    /* e.g. "Admin", "Employee". */
    public string DisplayName { get; set; } = null!;
    /* ABP's "admin" role, or a role that can manage everyone's bookings. */
    public bool IsAdmin { get; set; }
}
