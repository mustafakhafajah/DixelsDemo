namespace Dixels.Portal.Users;

/* The two roles the user directory hands out. "admin" is ABP's own role (it holds every permission);
 * "employee" is seeded by RoleDataSeedContributor. */
public static class UserDirectoryRoles
{
    public const string Admin = "admin";
    public const string Employee = "employee";

    public static readonly string[] All = { Admin, Employee };
}
