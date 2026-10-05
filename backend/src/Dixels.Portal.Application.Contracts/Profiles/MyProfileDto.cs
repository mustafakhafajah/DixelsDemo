using System;
using Volo.Abp.Account;

namespace Dixels.Portal.Profiles;

/* ABP's own profile (name, user name, email, phone, sign-in method and extra properties such as the address),
 * plus what ABP's profile leaves out: whether email and phone are confirmed, roles, organisation and dates. */
public class MyProfileDto
{
    public ProfileDto Profile { get; set; } = null!;

    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }

    /* Role names, sorted; empty when the user has none. */
    public string[] Roles { get; set; } = [];

    /* The tenant's name; null for a user of the host (no tenant). */
    public string? TenantName { get; set; }

    public DateTime CreationTime { get; set; }

    /* Null when the user has never signed in. */
    public DateTime? LastSignInTime { get; set; }

    /* Null when the password was never changed, or the user has none (external sign-in only). */
    public DateTimeOffset? LastPasswordChangeTime { get; set; }

    /* Changes whenever the profile picture does, so the SPA fetches the new one at once; null when there is none. */
    public string? PictureVersion { get; set; }
}
