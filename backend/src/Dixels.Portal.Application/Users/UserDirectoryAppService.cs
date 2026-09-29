using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Dixels.Portal.Users;

/* The admin user directory, view only: who has an account, their roles, and whether they are active or locked.
 * Accounts, roles and permissions are changed in ABP's own Identity pages (/Identity/Users, /Identity/Roles). */
[Authorize]
public class UserDirectoryAppService : PortalAppService, IUserDirectoryAppService
{
    private const int MaxPageSize = 100;

    private readonly IIdentityUserRepository _users;
    private readonly IIdentityRoleRepository _roles;
    private readonly IPermissionGrantRepository _grants;

    public UserDirectoryAppService(IIdentityUserRepository users, IIdentityRoleRepository roles, IPermissionGrantRepository grants)
    {
        _users = users;
        _roles = roles;
        _grants = grants;
    }

    /* Uses ABP's own "view users" permission (Identity management > Users), so there is no extra permission to manage.
     * Filtered in memory: the directory is small, and it keeps the filter case-insensitive on every database. */
    [Authorize(IdentityPermissions.Users.Default)]
    public async Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(UserDirectoryListInput input)
    {
        var role = string.IsNullOrWhiteSpace(input.Role) ? null : await ResolveRoleAsync(input.Role);
        var roleNames = (await _roles.GetListAsync()).ToDictionary(r => r.Id, r => r.Name);
        var all = await _users.GetListAsync(includeDetails: true);
        var roles = all.ToDictionary(u => u.Id, u => OrderRoles(u.Roles
            .Select(r => roleNames.GetValueOrDefault(r.RoleId))
            .Where(n => n != null)
            .Select(n => n!)));
        IEnumerable<IdentityUser> users = all;

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim();
            users = users.Where(u => Matches(u.GetDisplayName(), filter) || Matches(u.Name, filter) ||
                                     Matches(u.Surname, filter) || Matches(u.UserName, filter) || Matches(u.Email, filter));
        }
        if (role != null) users = users.Where(u => roles[u.Id].Contains(role));
        if (input.IsActive.HasValue) users = users.Where(u => u.IsActive == input.IsActive.Value);
        if (input.IsLocked.HasValue) users = users.Where(u => IsLocked(u) == input.IsLocked.Value);

        var ordered = users
            .OrderBy(u => u.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var page = ordered
            .Skip(Math.Max(0, input.SkipCount))
            .Take(Math.Clamp(input.MaxResultCount, 1, MaxPageSize))
            .Select(u => Map(u, roles[u.Id]))
            .ToList();
        return new PagedResultDto<UserDirectoryItemDto>(ordered.Count, page);
    }

    /* Every role, admin first then by name. A role counts as an admin role when it is ABP's "admin" or it can
     * manage everyone's bookings. */
    [Authorize(IdentityPermissions.Users.Default)]
    public async Task<ListResultDto<UserDirectoryRoleDto>> GetRolesAsync()
    {
        var items = new List<UserDirectoryRoleDto>();
        foreach (var role in await _roles.GetListAsync())
        {
            var isAdmin = IsAdminRole(role.Name) ||
                          await _grants.FindAsync(PortalPermissions.Bookings.ManageAll, RolePermissionValueProvider.ProviderName, role.Name) != null;
            items.Add(new UserDirectoryRoleDto { Name = role.Name, DisplayName = RoleDisplayName(role.Name), IsAdmin = isAdmin });
        }
        return new ListResultDto<UserDirectoryRoleDto>(items
            .OrderBy(r => IsAdminRole(r.Name) ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /* roles: already in OrderRoles order, so the first one is the user's main role. */
    private static UserDirectoryItemDto Map(IdentityUser user, string[] roles)
    {
        var locked = IsLocked(user);
        return new UserDirectoryItemDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            UserName = user.UserName,
            Email = user.Email,
            Role = roles.FirstOrDefault(),
            Roles = roles,
            IsActive = user.IsActive,
            LockoutEnd = locked ? user.LockoutEnd!.Value.UtcDateTime : null,
            IsLocked = locked,
        };
    }

    /* Same rule as Identity's own sign-in check. */
    private static bool IsLocked(IdentityUser user) =>
        user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private static bool Matches(string? value, string filter) =>
        value != null && value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /* The existing role's own name for what was sent, ignoring case. */
    private async Task<string> ResolveRoleAsync(string role)
    {
        var wanted = role.Trim();
        return (await _roles.GetListAsync()).FirstOrDefault(r => string.Equals(r.Name, wanted, StringComparison.OrdinalIgnoreCase))?.Name
               ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.UserInvalidRole, message: "That role doesn't exist.");
    }

    private static bool IsAdminRole(string role) => string.Equals(role, UserDirectoryRoles.Admin, StringComparison.OrdinalIgnoreCase);

    /* "admin" first, then by name. */
    private static string[] OrderRoles(IEnumerable<string> roles) => roles
        .OrderBy(r => IsAdminRole(r) ? 0 : 1)
        .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /* "employee" -> "Employee", "front_desk" -> "Front desk". */
    private static string RoleDisplayName(string role)
    {
        var words = role.Replace('_', ' ').Replace('-', ' ').Trim();
        return words.Length == 0 ? role : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
