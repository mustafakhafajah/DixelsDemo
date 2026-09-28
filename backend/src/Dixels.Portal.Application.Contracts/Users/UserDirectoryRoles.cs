namespace Dixels.Portal.Users;

/* Role names the user directory relies on. Any ABP role can be handed out; "admin" is ABP's own role
 * (it holds every permission, and the last active admin is protected), "employee" is seeded by
 * RoleDataSeedContributor and is the usual role for a new user. */
public static class UserDirectoryRoles
{
    public const string Admin = "admin";
    public const string Employee = "employee";
}
