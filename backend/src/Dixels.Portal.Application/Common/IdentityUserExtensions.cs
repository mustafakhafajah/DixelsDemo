using Volo.Abp.Identity;

namespace Dixels.Portal.Common;

public static class IdentityUserExtensions
{
    /* "Name Surname", falling back to the user name when both are empty. */
    public static string GetDisplayName(this IdentityUser user)
    {
        var full = $"{user.Name} {user.Surname}".Trim();
        return full.Length > 0 ? full : user.UserName;
    }
}
