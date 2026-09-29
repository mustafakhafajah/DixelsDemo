namespace Dixels.Portal.Users;

/* Role names the user directory relies on. "admin" is ABP's own role (it holds every permission and is
 * shown first), "employee" is seeded by RoleDataSeedContributor. */
public static class UserDirectoryRoles
{
    public const string Admin = "admin";
    public const string Employee = "employee";
}
