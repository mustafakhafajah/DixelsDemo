using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Profiles;

[Authorize]
public class ProfileLookupAppService : EstateAppServiceBase, IProfileLookupAppService
{
    private readonly IIdentityUserRepository _users;

    public ProfileLookupAppService(IIdentityUserRepository users)
    {
        _users = users;
    }

    public async Task<CurrentUserProfileDto> GetCurrentAsync()
    {
        var user = await _users.GetAsync(CurrentUser.GetId(), includeDetails: false);
        return new CurrentUserProfileDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            Email = user.Email,
            IsAdmin = await IsAdminAsync(),
        };
    }

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public async Task<ListResultDto<UserLookupDto>> GetUsersAsync()
    {
        var users = await _users.GetListAsync(includeDetails: true);
        var adminRoles = await _users.GetRoleNamesAsync(users.Select(u => u.Id));
        var admins = adminRoles.Where(r => r.RoleNames.Contains("admin")).Select(r => r.Id).ToHashSet();
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(u => u.GetDisplayName())
            .Select(u => new UserLookupDto { Id = u.Id, Name = u.GetDisplayName(), IsAdmin = admins.Contains(u.Id) })
            .ToList());
    }
}
