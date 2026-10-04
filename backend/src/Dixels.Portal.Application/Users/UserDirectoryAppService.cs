using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace Dixels.Portal.Users;

/* The read-only user directory. Users are still managed in ABP's own Users page; this only reads them through
 * ABP's user repository, whose search and role / lock / active filters run in the database. ABP's public
 * /api/identity/users only takes a search text, which is why this thin endpoint exists. */
[Authorize(IdentityPermissions.Users.Default)]
public class UserDirectoryAppService : PortalAppService, IUserDirectoryAppService
{
    /* Same order the page has always shown: by name, then surname, then user name. */
    private const string Sorting = nameof(IdentityUser.Name) + ", " + nameof(IdentityUser.Surname) + ", " + nameof(IdentityUser.UserName);

    private readonly IIdentityUserRepository _users;
    private readonly IIdentityRoleRepository _roles;

    public UserDirectoryAppService(IIdentityUserRepository users, IIdentityRoleRepository roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(GetUserDirectoryInput input)
    {
        var filter = string.IsNullOrWhiteSpace(input.Filter) ? null : input.Filter.Trim();
        var notActive = input.IsActive.HasValue ? !input.IsActive.Value : (bool?)null;

        var total = await _users.GetCountAsync(filter, input.RoleId, isLockedOut: input.IsLocked, notActive: notActive);
        var users = await _users.GetListAsync(Sorting, input.MaxResultCount, input.SkipCount, filter,
            includeDetails: true, roleId: input.RoleId, isLockedOut: input.IsLocked, notActive: notActive);

        /* One query for every role name, instead of one request per user. */
        var roleNames = (await _roles.GetListAsync()).ToDictionary(r => r.Id, r => r.Name);
        var now = Clock.Now;

        return new PagedResultDto<UserDirectoryItemDto>(total, users.Select(u =>
        {
            var locked = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd.Value > now;
            return new UserDirectoryItemDto
            {
                Id = u.Id,
                Name = u.GetDisplayName(),
                UserName = u.UserName,
                Email = u.Email,
                IsActive = u.IsActive,
                IsLocked = locked,
                LockoutEnd = locked ? u.LockoutEnd : null,
                Roles = u.Roles.Select(r => roleNames.GetValueOrDefault(r.RoleId)).OfType<string>().ToList(),
            };
        }).ToList());
    }
}
