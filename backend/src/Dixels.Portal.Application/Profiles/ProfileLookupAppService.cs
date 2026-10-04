using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Profiles;

/* Who is signed in, and the people list for the Schedule's person filter. What anyone may do is never decided
 * here: every screen and service asks for the exact permission it needs (Bookings.ViewAll, EditAll, ...). */
[Authorize]
public class ProfileLookupAppService : PortalAppService, IProfileLookupAppService
{
    private readonly IIdentityUserRepository _users;
    private readonly IIdentityRoleRepository _roles;

    public ProfileLookupAppService(IIdentityUserRepository users, IIdentityRoleRepository roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<CurrentUserProfileDto> GetCurrentAsync()
    {
        var user = await _users.GetAsync(CurrentUser.GetId(), includeDetails: false);
        return new CurrentUserProfileDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            Email = user.Email,
            Roles = (await _users.GetRoleNamesAsync(user.Id)).OrderBy(r => r).ToList(),
        };
    }

    /* Every user with a display name and role names, for the Schedule's person filter - reading other people's
     * bookings, so Bookings.ViewAll. All role names come from one query. */
    [Authorize(PortalPermissions.Bookings.ViewAll)]
    public async Task<ListResultDto<UserLookupDto>> GetUsersAsync()
    {
        var users = await _users.GetListAsync(includeDetails: true);
        var roleNames = (await _roles.GetListAsync()).ToDictionary(r => r.Id, r => r.Name);
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(u => u.GetDisplayName())
            .Select(u => new UserLookupDto
            {
                Id = u.Id,
                Name = u.GetDisplayName(),
                Roles = u.Roles.Select(r => roleNames.GetValueOrDefault(r.RoleId)).OfType<string>().OrderBy(n => n).ToList(),
            })
            .ToList());
    }
}
