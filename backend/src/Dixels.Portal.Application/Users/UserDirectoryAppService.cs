using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Identity;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Dixels.Portal.Users;

/* The admin user directory: list, add, change role, lock / unlock, activate / deactivate, and give or
 * take away single Portal permissions for one user.
 *
 * ABP permissions only add up, so "taking away" a permission the user's role grants is stored as a
 * block ("UB" grant, see UserBlockPermissionValueProvider); "giving" one the role lacks is an ordinary
 * user grant ("U"). Changing the role starts the user afresh: both kinds are cleared.
 *
 * Guard rails, checked in code: nobody can lock, deactivate or change the role of themselves, or take
 * their own user directory permissions away; and the last active, unlocked admin can't be locked,
 * deactivated or demoted, so there is always someone who can undo a mistake. */
[Authorize]
public class UserDirectoryAppService : PortalAppService, IUserDirectoryAppService
{
    private const int MaxPageSize = 100;
    private const string PortalPrefix = PortalPermissions.GroupName + ".";

    private readonly IIdentityUserRepository _users;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IPermissionGrantRepository _grants;
    private readonly IPermissionDefinitionManager _definitions;

    public UserDirectoryAppService(IIdentityUserRepository users, IdentityUserManager userManager,
        IPermissionManager permissionManager, IPermissionGrantRepository grants, IPermissionDefinitionManager definitions)
    {
        _users = users;
        _userManager = userManager;
        _permissionManager = permissionManager;
        _grants = grants;
        _definitions = definitions;
    }

    /* Filtered in memory: the directory is small, and it keeps the filter case-insensitive on every database. */
    [Authorize(PortalPermissions.Users.Default)]
    public async Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(UserDirectoryListInput input)
    {
        var role = string.IsNullOrWhiteSpace(input.Role) ? null : NormalizeRole(input.Role);
        var roles = await GetRoleMapAsync();
        IEnumerable<IdentityUser> users = await _users.GetListAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim();
            users = users.Where(u => Matches(u.GetDisplayName(), filter) || Matches(u.Name, filter) ||
                                     Matches(u.Surname, filter) || Matches(u.UserName, filter) || Matches(u.Email, filter));
        }
        if (role != null) users = users.Where(u => roles.GetValueOrDefault(u.Id) == role);
        if (input.IsActive.HasValue) users = users.Where(u => u.IsActive == input.IsActive.Value);
        if (input.IsLocked.HasValue) users = users.Where(u => IsLocked(u) == input.IsLocked.Value);

        var ordered = users
            .OrderBy(u => u.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var page = ordered
            .Skip(Math.Max(0, input.SkipCount))
            .Take(Math.Clamp(input.MaxResultCount, 1, MaxPageSize))
            .Select(u => Map(u, roles.GetValueOrDefault(u.Id)))
            .ToList();
        return new PagedResultDto<UserDirectoryItemDto>(ordered.Count, page);
    }

    [Authorize(PortalPermissions.Users.Create)]
    public async Task<UserDirectoryItemDto> CreateAsync(CreateUserDirectoryDto input)
    {
        var role = NormalizeRole(input.Role);
        var email = input.Email.Trim();
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? email : input.UserName.Trim();

        var user = new IdentityUser(GuidGenerator.Create(), userName, email, CurrentTenant.Id)
        {
            Name = input.Name.Trim()
        };
        user.SetEmailConfirmed(true);
        Check(await _userManager.CreateAsync(user, input.Password), "Could not add the user");
        Check(await _userManager.AddToRoleAsync(user, role), "Could not give the user their role");
        return await MapAsync(user);
    }

    /* The user ends up with exactly one of admin / employee, and loses any permissions given or taken away by hand. */
    [Authorize(PortalPermissions.Users.Edit)]
    public async Task<UserDirectoryItemDto> SetRoleAsync(Guid id, SetUserRoleDto input)
    {
        var role = NormalizeRole(input.Role);
        var user = await GetUserAsync(id);
        EnsureNotSelf(user, "You can't change your own role.");
        if (role != UserDirectoryRoles.Admin)
            await EnsureNotLastAdminAsync(user, "demoted");

        foreach (var other in UserDirectoryRoles.All.Where(r => r != role))
        {
            if (await _userManager.IsInRoleAsync(user, other))
                Check(await _userManager.RemoveFromRoleAsync(user, other), "Could not change the role");
        }
        if (!await _userManager.IsInRoleAsync(user, role))
            Check(await _userManager.AddToRoleAsync(user, role), "Could not change the role");

        await ClearOverridesAsync(user.Id);
        return await MapAsync(user);
    }

    /* No end time locks the user until someone unlocks them. */
    [Authorize(PortalPermissions.Users.Edit)]
    public async Task<UserDirectoryItemDto> LockAsync(Guid id, LockUserDto input)
    {
        var user = await GetUserAsync(id);
        EnsureNotSelf(user, "You can't lock yourself.");

        var until = DateTimeOffset.MaxValue;
        if (input.Until.HasValue)
        {
            var utc = input.Until.Value.Kind == DateTimeKind.Local
                ? input.Until.Value.ToUniversalTime()
                : DateTime.SpecifyKind(input.Until.Value, DateTimeKind.Utc);
            if (utc <= DateTime.UtcNow)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.UserInvalidLock,
                    message: "Pick a time in the future to lock the user until.");
            until = new DateTimeOffset(utc);
        }

        await EnsureNotLastAdminAsync(user, "locked");
        Check(await _userManager.SetLockoutEnabledAsync(user, true), "Could not lock the user");
        Check(await _userManager.SetLockoutEndDateAsync(user, until), "Could not lock the user");
        /* Signs them out of sessions they already have. */
        Check(await _userManager.UpdateSecurityStampAsync(user), "Could not lock the user");
        return await MapAsync(user);
    }

    [Authorize(PortalPermissions.Users.Edit)]
    public async Task<UserDirectoryItemDto> UnlockAsync(Guid id)
    {
        var user = await GetUserAsync(id);
        if (user.LockoutEnd.HasValue)
        {
            /* Identity only changes the lockout end while lockout is enabled. */
            if (!user.LockoutEnabled)
                Check(await _userManager.SetLockoutEnabledAsync(user, true), "Could not unlock the user");
            Check(await _userManager.SetLockoutEndDateAsync(user, null), "Could not unlock the user");
        }
        Check(await _userManager.ResetAccessFailedCountAsync(user), "Could not unlock the user");
        return await MapAsync(user);
    }

    [Authorize(PortalPermissions.Users.Edit)]
    public async Task<UserDirectoryItemDto> SetActiveAsync(Guid id, SetUserActiveDto input)
    {
        var user = await GetUserAsync(id);
        if (!input.IsActive)
        {
            EnsureNotSelf(user, "You can't deactivate yourself.");
            await EnsureNotLastAdminAsync(user, "deactivated");
        }

        if (user.IsActive != input.IsActive)
        {
            user.SetIsActive(input.IsActive);
            Check(await _userManager.UpdateAsync(user), "Could not change the user");
            if (!input.IsActive)
                Check(await _userManager.UpdateSecurityStampAsync(user), "Could not change the user");
        }
        return await MapAsync(user);
    }

    [Authorize(PortalPermissions.Users.ManagePermissions)]
    public async Task<ListResultDto<UserPermissionDto>> GetPermissionsAsync(Guid id)
    {
        var user = await GetUserAsync(id);
        return new ListResultDto<UserPermissionDto>(await GetPermissionListAsync(user));
    }

    /* Want it: drop any block, and give it to the user unless a role already does.
     * Don't want it: drop any user grant, and block it if a role grants it. Sending the same list twice changes nothing. */
    [Authorize(PortalPermissions.Users.ManagePermissions)]
    public async Task<ListResultDto<UserPermissionDto>> UpdatePermissionsAsync(Guid id, UpdateUserPermissionsDto input)
    {
        var user = await GetUserAsync(id);
        var current = (await GetPermissionListAsync(user)).ToDictionary(p => p.Name);
        var wanted = input.Permissions
            .GroupBy(p => p.Name)
            .Select(g => g.Last())
            .ToList();

        var unknown = wanted.FirstOrDefault(p => !current.ContainsKey(p.Name));
        if (unknown != null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserInvalidPermission,
                message: $"'{unknown.Name}' is not a portal permission that can be set here.");

        if (IsSelf(user) && wanted.Any(p => !p.IsGranted && IsUserDirectoryPermission(p.Name) && current[p.Name].IsGranted))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserSelfChange,
                message: "You can't take the user directory permissions away from yourself.");

        var key = user.Id.ToString();
        foreach (var p in wanted)
        {
            var fromRole = current[p.Name].FromRole;
            if (p.IsGranted)
            {
                await _permissionManager.SetAsync(p.Name, UserBlockPermissionValueProvider.ProviderName, key, false);
                if (!fromRole) await _permissionManager.SetForUserAsync(user.Id, p.Name, true);
            }
            else
            {
                await _permissionManager.SetForUserAsync(user.Id, p.Name, false);
                if (fromRole) await _permissionManager.SetAsync(p.Name, UserBlockPermissionValueProvider.ProviderName, key, true);
            }
        }

        /* So the list below reads what was just written. */
        if (CurrentUnitOfWork != null) await CurrentUnitOfWork.SaveChangesAsync();
        return new ListResultDto<UserPermissionDto>(await GetPermissionListAsync(user));
    }

    /* Every Portal permission in definition order, with what this user ends up with and why. */
    private async Task<List<UserPermissionDto>> GetPermissionListAsync(IdentityUser user)
    {
        var definitions = new List<(PermissionDefinition Permission, string Group)>();
        foreach (var group in await _definitions.GetGroupsAsync())
        {
            definitions.AddRange(group.GetPermissionsWithChildren()
                .Where(p => p.IsEnabled && p.Name.StartsWith(PortalPrefix))
                .Select(p => (p, group.Name)));
        }
        var names = definitions.Select(d => d.Permission.Name).ToArray();
        if (names.Length == 0) return new List<UserPermissionDto>();

        var fromRole = new HashSet<string>();
        foreach (var role in await _userManager.GetRolesAsync(user))
        {
            fromRole.UnionWith((await _grants.GetListAsync(names, RolePermissionValueProvider.ProviderName, role))
                .Select(g => g.Name));
        }
        var key = user.Id.ToString();
        var given = (await _grants.GetListAsync(names, UserPermissionValueProvider.ProviderName, key))
            .Select(g => g.Name).ToHashSet();
        var blocked = (await _grants.GetListAsync(names, UserBlockPermissionValueProvider.ProviderName, key))
            .Select(g => g.Name).ToHashSet();

        return definitions.Select(d =>
        {
            var name = d.Permission.Name;
            var isBlocked = blocked.Contains(name);
            var isFromRole = fromRole.Contains(name);
            var isGiven = given.Contains(name);
            return new UserPermissionDto
            {
                Name = name,
                DisplayName = d.Permission.DisplayName.Localize(StringLocalizerFactory).Value,
                ParentName = d.Permission.Parent?.Name,
                GroupName = d.Group,
                IsGranted = !isBlocked && (isFromRole || isGiven),
                FromRole = isFromRole,
                Source = isBlocked ? UserPermissionSources.Blocked
                    : isFromRole ? UserPermissionSources.Role
                    : isGiven ? UserPermissionSources.User
                    : UserPermissionSources.None,
            };
        }).ToList();
    }

    /* A new role starts from what that role grants: remove the Portal permissions given or taken away by hand. */
    private async Task ClearOverridesAsync(Guid userId)
    {
        var key = userId.ToString();
        foreach (var provider in new[] { UserPermissionValueProvider.ProviderName, UserBlockPermissionValueProvider.ProviderName })
        {
            foreach (var grant in (await _grants.GetListAsync(provider, key)).Where(g => g.Name.StartsWith(PortalPrefix)))
                await _grants.DeleteAsync(grant);
        }
    }

    /* The last active, unlocked admin keeps the job, so someone can always manage users. */
    private async Task EnsureNotLastAdminAsync(IdentityUser user, string what)
    {
        if (!user.IsActive || IsLocked(user) || !await _userManager.IsInRoleAsync(user, UserDirectoryRoles.Admin))
            return;

        var admins = await _userManager.GetUsersInRoleAsync(UserDirectoryRoles.Admin);
        if (!admins.Any(a => a.Id != user.Id && a.IsActive && !IsLocked(a)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserLastAdmin,
                message: $"{user.GetDisplayName()} is the last active admin and can't be {what}. Make someone else an admin first.");
    }

    private void EnsureNotSelf(IdentityUser user, string message)
    {
        if (IsSelf(user))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserSelfChange, message: message);
    }

    private bool IsSelf(IdentityUser user) => CurrentUser.Id == user.Id;

    private static bool IsUserDirectoryPermission(string name) =>
        name == PortalPermissions.Users.Default || name.StartsWith(PortalPermissions.Users.Default + ".");

    private async Task<IdentityUser> GetUserAsync(Guid id)
    {
        return await _userManager.FindByIdAsync(id.ToString())
               ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.UserNotFound, message: "No user with that ID.");
    }

    /* Admin wins over employee for someone who somehow has both. */
    private async Task<Dictionary<Guid, string>> GetRoleMapAsync()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var u in await _userManager.GetUsersInRoleAsync(UserDirectoryRoles.Employee))
            map[u.Id] = UserDirectoryRoles.Employee;
        foreach (var u in await _userManager.GetUsersInRoleAsync(UserDirectoryRoles.Admin))
            map[u.Id] = UserDirectoryRoles.Admin;
        return map;
    }

    private async Task<UserDirectoryItemDto> MapAsync(IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.Contains(UserDirectoryRoles.Admin, StringComparer.OrdinalIgnoreCase) ? UserDirectoryRoles.Admin
            : roles.Contains(UserDirectoryRoles.Employee, StringComparer.OrdinalIgnoreCase) ? UserDirectoryRoles.Employee
            : null;
        return Map(user, role);
    }

    private static UserDirectoryItemDto Map(IdentityUser user, string? role)
    {
        var locked = IsLocked(user);
        return new UserDirectoryItemDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            UserName = user.UserName,
            Email = user.Email,
            Role = role,
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

    private static string NormalizeRole(string role)
    {
        var normalized = role.Trim().ToLowerInvariant();
        if (!UserDirectoryRoles.All.Contains(normalized))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserInvalidRole,
                message: "The role must be admin or employee.");
        return normalized;
    }

    /* Identity reports problems (taken email, weak password, ...) as a result; show them as one sentence. */
    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UserIdentityError,
                message: $"{what}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
