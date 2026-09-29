using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;

namespace Dixels.Portal.Profiles;

/* "Admin" everywhere means holding Bookings.ManageAll (directly or through a role). */
[Authorize]
public class ProfileLookupAppService : PortalAppService, IProfileLookupAppService
{
    private readonly IIdentityUserRepository _users;
    private readonly IPermissionFinder _permissionFinder;

    public ProfileLookupAppService(IIdentityUserRepository users, IPermissionFinder permissionFinder)
    {
        _users = users;
        _permissionFinder = permissionFinder;
    }

    public async Task<CurrentUserProfileDto> GetCurrentAsync()
    {
        var user = await _users.GetAsync(CurrentUser.GetId(), includeDetails: false);
        return new CurrentUserProfileDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            Email = user.Email,
            IsAdmin = await AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.ManageAll),
            Roles = (await _users.GetRoleNamesAsync(user.Id)).OrderBy(r => r).ToList(),
        };
    }

    /* Every user with a display name and an admin flag, for admin pickers. */
    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public async Task<ListResultDto<UserLookupDto>> GetUsersAsync()
    {
        var users = await _users.GetListAsync();
        var grants = users.Count == 0
            ? new()
            : await _permissionFinder.IsGrantedAsync(users
                .Select(u => new IsGrantedRequest { UserId = u.Id, PermissionNames = new[] { PortalPermissions.Bookings.ManageAll } })
                .ToList());
        var admins = grants
            .Where(g => g.Permissions.TryGetValue(PortalPermissions.Bookings.ManageAll, out var granted) && granted)
            .Select(g => g.UserId)
            .ToHashSet();
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(u => u.GetDisplayName())
            .Select(u => new UserLookupDto { Id = u.Id, Name = u.GetDisplayName(), IsAdmin = admins.Contains(u.Id) })
            .ToList());
    }
}
