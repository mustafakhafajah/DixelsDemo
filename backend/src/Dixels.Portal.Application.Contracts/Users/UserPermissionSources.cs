namespace Dixels.Portal.Users;

/* Where a user's permission comes from, as UserPermissionDto.Source. */
public static class UserPermissionSources
{
    /* Granted by one of the user's roles. */
    public const string Role = "role";
    /* Given to this user on top of their role. */
    public const string User = "user";
    /* The role grants it, but it is taken away from this user. */
    public const string Blocked = "blocked";
    public const string None = "none";
}
