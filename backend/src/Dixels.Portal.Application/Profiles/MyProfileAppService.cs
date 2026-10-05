using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Account;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Profiles;

/* Any signed-in user may read their own record; there is no way to ask for anyone else's. ABP's profile is
 * reused as it is, and only what it leaves out is read here, from ABP's user, role and security-log stores. */
[Authorize]
public class MyProfileAppService : PortalAppService, IMyProfileAppService
{
    /* Newest first, so the first entry is the latest sign-in. */
    private const string NewestFirst = nameof(IdentitySecurityLog.CreationTime) + " desc";

    private readonly IProfileAppService _profile;
    private readonly IdentityUserManager _users;
    private readonly IIdentitySecurityLogRepository _securityLogs;

    public MyProfileAppService(IProfileAppService profile, IdentityUserManager users, IIdentitySecurityLogRepository securityLogs)
    {
        _profile = profile;
        _users = users;
        _securityLogs = securityLogs;
    }

    public async Task<MyProfileDto> GetAsync()
    {
        var user = await _users.GetByIdAsync(CurrentUser.GetId());

        var dto = ObjectMapper.Map<IdentityUser, MyProfileDto>(user);
        dto.Profile = await _profile.GetAsync();
        dto.Roles = (await _users.GetRolesAsync(user)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        dto.TenantName = CurrentTenant.Name;
        dto.LastSignInTime = await GetLastSignInTimeAsync(user);
        dto.PictureVersion = user.GetProperty<string?>(ProfilePictureConsts.VersionPropertyName);
        return dto;
    }

    /* ABP's sign-in page records every successful sign-in in its security log, but sets the user's own
     * LastSignInTime only for the password grant; so the log comes first, and the user's field covers the rest. */
    private async Task<DateTime?> GetLastSignInTimeAsync(IdentityUser user)
    {
        var latest = await _securityLogs.GetListAsync(
            sorting: NewestFirst,
            maxResultCount: 1,
            action: IdentitySecurityLogActionConsts.LoginSucceeded,
            userId: user.Id);

        return latest.FirstOrDefault()?.CreationTime ?? user.LastSignInTime?.UtcDateTime;
    }
}
