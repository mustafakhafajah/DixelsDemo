using System;
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
    /* ABP's built-in administrator role; only used to label people in the list. */
    private const string AdminRoleName = "admin";

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

    /* Every user with a display name, for the Schedule's person filter - reading other people's bookings, so
     * Bookings.ViewAll. IsAdmin only labels people in the admin role. */
    [Authorize(PortalPermissions.Bookings.ViewAll)]
    public async Task<ListResultDto<UserLookupDto>> GetUsersAsync()
    {
        var users = await _users.GetListAsync(includeDetails: true);
        var adminRole = (await _roles.GetListAsync())
            .FirstOrDefault(r => string.Equals(r.Name, AdminRoleName, StringComparison.OrdinalIgnoreCase));
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(u => u.GetDisplayName())
            .Select(u => new UserLookupDto
            {
                Id = u.Id,
                Name = u.GetDisplayName(),
                IsAdmin = adminRole != null && u.Roles.Any(r => r.RoleId == adminRole.Id),
            })
            .ToList());
    }
}
