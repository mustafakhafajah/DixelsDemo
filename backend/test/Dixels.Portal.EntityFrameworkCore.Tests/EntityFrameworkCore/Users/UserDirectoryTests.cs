using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Portal.Identity;
using Dixels.Portal.Permissions;
using Dixels.Portal.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using IdentityUserManager = Volo.Abp.Identity.IdentityUserManager;

namespace Dixels.Portal.EntityFrameworkCore.Users;

/* The admin user directory. The test database is seeded with the "admin" user (admin role) and
 * employee@dixels.io (employee role). Tests allow every [Authorize], so the permission checks that
 * matter here go through ABP's real PermissionChecker with a principal made for the user. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class UserDirectoryTests : PortalEntityFrameworkCoreTestBase
{
    private const string Password = "1q2w3E*";

    private readonly IUserDirectoryAppService _directory;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionGrantRepository _grants;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly IPermissionChecker _checker;

    public UserDirectoryTests()
    {
        _directory = GetRequiredService<IUserDirectoryAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _grants = GetRequiredService<IPermissionGrantRepository>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        /* The concrete checker: IPermissionChecker itself is replaced by "always allow" in tests. */
        _checker = GetRequiredService<PermissionChecker>();
    }

    [Fact]
    public async Task Lists_everyone_ordered_by_name_with_their_role()
    {
        var list = await _directory.GetListAsync(new UserDirectoryListInput());

        list.TotalCount.ShouldBe(2);
        list.Items.Select(u => u.Name).ShouldBe(new[] { "admin", "Employee" });
        list.Items.Single(u => u.UserName == "admin").Role.ShouldBe(UserDirectoryRoles.Admin);
        var employee = list.Items.Single(u => u.Email == RoleDataSeedContributor.EmployeeEmail);
        employee.Role.ShouldBe(UserDirectoryRoles.Employee);
        employee.IsActive.ShouldBeTrue();
        employee.IsLocked.ShouldBeFalse();
        employee.LockoutEnd.ShouldBeNull();
    }

    [Fact]
    public async Task Filters_by_text_role_status_and_lock()
    {
        var zed = await CreateAsync("Zed Quinn", "zed@dixels.io", UserDirectoryRoles.Employee);
        var amy = await CreateAsync("Amy Admin", "amy@dixels.io", UserDirectoryRoles.Admin);

        (await ListAsync(new UserDirectoryListInput { Filter = "QUINN" })).ShouldBe(new[] { zed.Id });
        (await ListAsync(new UserDirectoryListInput { Filter = "ZED@DIXELS" })).ShouldBe(new[] { zed.Id });
        (await ListAsync(new UserDirectoryListInput { Role = "admin" })).Count.ShouldBe(2);
        (await ListAsync(new UserDirectoryListInput { Role = "employee", Filter = "amy" })).ShouldBeEmpty();

        await _directory.SetActiveAsync(zed.Id, new SetUserActiveDto { IsActive = false });
        await _directory.LockAsync(amy.Id, new LockUserDto());

        (await ListAsync(new UserDirectoryListInput { IsActive = false })).ShouldBe(new[] { zed.Id });
        (await ListAsync(new UserDirectoryListInput { IsLocked = true })).ShouldBe(new[] { amy.Id });
        (await ListAsync(new UserDirectoryListInput { IsLocked = false, IsActive = true })).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Pages_through_the_list()
    {
        for (var i = 0; i < 3; i++)
            await CreateAsync($"Paged {i}", $"paged{i}@dixels.io", UserDirectoryRoles.Employee);

        var first = await _directory.GetListAsync(new UserDirectoryListInput { MaxResultCount = 2 });
        var second = await _directory.GetListAsync(new UserDirectoryListInput { MaxResultCount = 2, SkipCount = 2 });

        first.TotalCount.ShouldBe(5);
        first.Items.Count.ShouldBe(2);
        second.Items.Count.ShouldBe(2);
        first.Items.Select(u => u.Id).Intersect(second.Items.Select(u => u.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Creates_a_confirmed_user_with_the_role()
    {
        var created = await CreateAsync("New Person", "new.person@dixels.io", UserDirectoryRoles.Employee);

        created.Name.ShouldBe("New Person");
        created.UserName.ShouldBe("new.person@dixels.io");
        created.Role.ShouldBe(UserDirectoryRoles.Employee);
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(created.Id);
            user.EmailConfirmed.ShouldBeTrue();
            (await _userManager.CheckPasswordAsync(user, Password)).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Create_reports_identity_problems_and_bad_roles()
    {
        var taken = await Should.ThrowAsync<UserFriendlyException>(() =>
            CreateAsync("Copy", RoleDataSeedContributor.EmployeeEmail, UserDirectoryRoles.Employee));
        taken.Code.ShouldBe(PortalDomainErrorCodes.UserIdentityError);
        taken.Message.ShouldContain("Could not add the user");

        var role = await Should.ThrowAsync<UserFriendlyException>(() =>
            CreateAsync("Boss", "boss@dixels.io", "superuser"));
        role.Code.ShouldBe(PortalDomainErrorCodes.UserInvalidRole);
    }

    [Fact]
    public async Task Setting_the_role_swaps_roles_and_clears_overrides()
    {
        var user = await CreateAsync("Role Changer", "role.changer@dixels.io", UserDirectoryRoles.Employee);
        await SetAsync(user.Id, (PortalPermissions.Bookings.Create, false), (PortalPermissions.Bookings.ManageAll, true));
        (await CountOverridesAsync(user.Id)).ShouldBe(2);

        var admin = await _directory.SetRoleAsync(user.Id, new SetUserRoleDto { Role = "Admin" });

        admin.Role.ShouldBe(UserDirectoryRoles.Admin);
        (await CountOverridesAsync(user.Id)).ShouldBe(0);
        await WithUnitOfWorkAsync(async () =>
        {
            var roles = await _userManager.GetRolesAsync(await _userManager.GetByIdAsync(user.Id));
            roles.ShouldBe(new[] { UserDirectoryRoles.Admin });
        });
        (await _checker.IsGrantedAsync(PrincipalFor(user.Id, UserDirectoryRoles.Admin), PortalPermissions.Bookings.Create))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Locks_and_unlocks()
    {
        var user = await CreateAsync("Lock Me", "lock.me@dixels.io", UserDirectoryRoles.Employee);
        var until = DateTime.UtcNow.AddDays(3);

        var locked = await _directory.LockAsync(user.Id, new LockUserDto { Until = until });
        locked.IsLocked.ShouldBeTrue();
        locked.LockoutEnd!.Value.ShouldBe(until, TimeSpan.FromSeconds(1));
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.IsLockedOutAsync(await _userManager.GetByIdAsync(user.Id))).ShouldBeTrue());

        var forever = await _directory.LockAsync(user.Id, new LockUserDto());
        forever.LockoutEnd!.Value.Year.ShouldBe(9999);

        var unlocked = await _directory.UnlockAsync(user.Id);
        unlocked.IsLocked.ShouldBeFalse();
        unlocked.LockoutEnd.ShouldBeNull();
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.IsLockedOutAsync(await _userManager.GetByIdAsync(user.Id))).ShouldBeFalse());

        var past = await Should.ThrowAsync<UserFriendlyException>(() =>
            _directory.LockAsync(user.Id, new LockUserDto { Until = DateTime.UtcNow.AddMinutes(-1) }));
        past.Code.ShouldBe(PortalDomainErrorCodes.UserInvalidLock);
    }

    [Fact]
    public async Task Deactivates_and_activates()
    {
        var user = await CreateAsync("Sleepy", "sleepy@dixels.io", UserDirectoryRoles.Employee);

        (await _directory.SetActiveAsync(user.Id, new SetUserActiveDto { IsActive = false })).IsActive.ShouldBeFalse();
        await WithUnitOfWorkAsync(async () => (await _userManager.GetByIdAsync(user.Id)).IsActive.ShouldBeFalse());

        (await _directory.SetActiveAsync(user.Id, new SetUserActiveDto { IsActive = true })).IsActive.ShouldBeTrue();
        await WithUnitOfWorkAsync(async () => (await _userManager.GetByIdAsync(user.Id)).IsActive.ShouldBeTrue());
    }

    [Fact]
    public async Task The_last_active_admin_cant_be_locked_deactivated_or_demoted()
    {
        var adminId = await GetAdminIdAsync();

        (await Should.ThrowAsync<UserFriendlyException>(() => _directory.LockAsync(adminId, new LockUserDto())))
            .Code.ShouldBe(PortalDomainErrorCodes.UserLastAdmin);
        (await Should.ThrowAsync<UserFriendlyException>(() =>
                _directory.SetActiveAsync(adminId, new SetUserActiveDto { IsActive = false })))
            .Code.ShouldBe(PortalDomainErrorCodes.UserLastAdmin);
        (await Should.ThrowAsync<UserFriendlyException>(() =>
                _directory.SetRoleAsync(adminId, new SetUserRoleDto { Role = UserDirectoryRoles.Employee })))
            .Code.ShouldBe(PortalDomainErrorCodes.UserLastAdmin);

        /* A second admin who is locked doesn't count either. */
        var second = await CreateAsync("Second Admin", "second.admin@dixels.io", UserDirectoryRoles.Admin);
        await _directory.LockAsync(second.Id, new LockUserDto());
        (await Should.ThrowAsync<UserFriendlyException>(() => _directory.LockAsync(adminId, new LockUserDto())))
            .Code.ShouldBe(PortalDomainErrorCodes.UserLastAdmin);

        await _directory.UnlockAsync(second.Id);
        (await _directory.LockAsync(adminId, new LockUserDto())).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task You_cant_lock_deactivate_demote_or_disempower_yourself()
    {
        var adminId = await GetAdminIdAsync();
        await CreateAsync("Other Admin", "other.admin@dixels.io", UserDirectoryRoles.Admin);

        using (_principal.Change(PrincipalFor(adminId, UserDirectoryRoles.Admin)))
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => _directory.LockAsync(adminId, new LockUserDto())))
                .Code.ShouldBe(PortalDomainErrorCodes.UserSelfChange);
            (await Should.ThrowAsync<UserFriendlyException>(() =>
                    _directory.SetActiveAsync(adminId, new SetUserActiveDto { IsActive = false })))
                .Code.ShouldBe(PortalDomainErrorCodes.UserSelfChange);
            (await Should.ThrowAsync<UserFriendlyException>(() =>
                    _directory.SetRoleAsync(adminId, new SetUserRoleDto { Role = UserDirectoryRoles.Employee })))
                .Code.ShouldBe(PortalDomainErrorCodes.UserSelfChange);
            (await Should.ThrowAsync<UserFriendlyException>(() =>
                    SetAsync(adminId, (PortalPermissions.Users.ManagePermissions, false))))
                .Code.ShouldBe(PortalDomainErrorCodes.UserSelfChange);

            /* Other permissions of your own are fine. */
            var list = await SetAsync(adminId, (PortalPermissions.Bookings.Create, false));
            list.Single(p => p.Name == PortalPermissions.Bookings.Create).Source.ShouldBe(UserPermissionSources.Blocked);
        }
    }

    [Fact]
    public async Task Taking_a_role_permission_away_denies_it_for_that_user_only()
    {
        var blocked = await CreateAsync("Blocked Employee", "blocked@dixels.io", UserDirectoryRoles.Employee);
        var other = await CreateAsync("Other Employee", "other@dixels.io", UserDirectoryRoles.Employee);
        var blockedPrincipal = PrincipalFor(blocked.Id, UserDirectoryRoles.Employee);
        var otherPrincipal = PrincipalFor(other.Id, UserDirectoryRoles.Employee);
        (await _checker.IsGrantedAsync(blockedPrincipal, PortalPermissions.Bookings.Create)).ShouldBeTrue();

        var list = await SetAsync(blocked.Id, (PortalPermissions.Bookings.Create, false));

        var entry = list.Single(p => p.Name == PortalPermissions.Bookings.Create);
        entry.IsGranted.ShouldBeFalse();
        entry.FromRole.ShouldBeTrue();
        entry.Source.ShouldBe(UserPermissionSources.Blocked);
        (await _checker.IsGrantedAsync(blockedPrincipal, PortalPermissions.Bookings.Create)).ShouldBeFalse();
        (await _checker.IsGrantedAsync(otherPrincipal, PortalPermissions.Bookings.Create)).ShouldBeTrue();

        /* The many-at-once check (what the SPA's grantedPolicies come from) agrees. */
        var many = await _checker.IsGrantedAsync(blockedPrincipal,
            new[] { PortalPermissions.Bookings.Default, PortalPermissions.Bookings.Create });
        many.Result[PortalPermissions.Bookings.Create].ShouldBe(PermissionGrantResult.Prohibited);
        many.Result[PortalPermissions.Bookings.Default].ShouldBe(PermissionGrantResult.Granted);

        /* Giving it back removes the block. */
        await SetAsync(blocked.Id, (PortalPermissions.Bookings.Create, true));
        (await _checker.IsGrantedAsync(blockedPrincipal, PortalPermissions.Bookings.Create)).ShouldBeTrue();
        (await CountOverridesAsync(blocked.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Giving_an_extra_permission_grants_it_to_that_user()
    {
        var employee = await CreateAsync("Extra Employee", "extra@dixels.io", UserDirectoryRoles.Employee);
        var principal = PrincipalFor(employee.Id, UserDirectoryRoles.Employee);
        (await _checker.IsGrantedAsync(principal, PortalPermissions.Bookings.ManageAll)).ShouldBeFalse();

        var list = await SetAsync(employee.Id, (PortalPermissions.Bookings.ManageAll, true));

        var entry = list.Single(p => p.Name == PortalPermissions.Bookings.ManageAll);
        entry.IsGranted.ShouldBeTrue();
        entry.FromRole.ShouldBeFalse();
        entry.Source.ShouldBe(UserPermissionSources.User);
        (await _checker.IsGrantedAsync(principal, PortalPermissions.Bookings.ManageAll)).ShouldBeTrue();

        await SetAsync(employee.Id, (PortalPermissions.Bookings.ManageAll, false));
        (await _checker.IsGrantedAsync(principal, PortalPermissions.Bookings.ManageAll)).ShouldBeFalse();
    }

    [Fact]
    public async Task Permission_list_covers_every_portal_permission_in_order()
    {
        var employeeId = (await ListAsync(new UserDirectoryListInput { Role = "employee" })).Single();

        var list = (await _directory.GetPermissionsAsync(employeeId)).Items;

        list.ShouldAllBe(p => p.Name.StartsWith("Portal."));
        list.Select(p => p.Name).ShouldContain(PortalPermissions.Users.ManagePermissions);
        var users = list.Single(p => p.Name == PortalPermissions.Users.Default);
        users.DisplayName.ShouldBe("User directory");
        users.Source.ShouldBe(UserPermissionSources.None);
        list.Single(p => p.Name == PortalPermissions.Users.Edit).ParentName.ShouldBe(PortalPermissions.Users.Default);
        list.Single(p => p.Name == PortalPermissions.Bookings.Create).Source.ShouldBe(UserPermissionSources.Role);
        list.Select(p => p.Name).ToList().IndexOf(PortalPermissions.Buildings.Default)
            .ShouldBeLessThan(list.Select(p => p.Name).ToList().IndexOf(PortalPermissions.Buildings.Create));
    }

    [Fact]
    public async Task Updating_permissions_twice_changes_nothing_the_second_time()
    {
        var employee = await CreateAsync("Twice", "twice@dixels.io", UserDirectoryRoles.Employee);
        var changes = new[] { (PortalPermissions.Bookings.Delete, false), (PortalPermissions.Maintenance.Create, true) };

        var first = await SetAsync(employee.Id, changes);
        var overrides = await CountOverridesAsync(employee.Id);
        var second = await SetAsync(employee.Id, changes);

        overrides.ShouldBe(2);
        (await CountOverridesAsync(employee.Id)).ShouldBe(2);
        second.Select(p => (p.Name, p.IsGranted, p.Source)).ShouldBe(first.Select(p => (p.Name, p.IsGranted, p.Source)));
    }

    [Fact]
    public async Task Only_portal_permissions_can_be_set()
    {
        var employeeId = (await ListAsync(new UserDirectoryListInput { Role = "employee" })).Single();

        (await Should.ThrowAsync<UserFriendlyException>(() => SetAsync(employeeId, ("AbpIdentity.Users", true))))
            .Code.ShouldBe(PortalDomainErrorCodes.UserInvalidPermission);
    }

    private Task<UserDirectoryItemDto> CreateAsync(string name, string email, string role) =>
        _directory.CreateAsync(new CreateUserDirectoryDto { Name = name, Email = email, Password = Password, Role = role });

    private async Task<List<Guid>> ListAsync(UserDirectoryListInput input) =>
        (await _directory.GetListAsync(input)).Items.Select(u => u.Id).ToList();

    private async Task<IReadOnlyList<UserPermissionDto>> SetAsync(Guid userId, params (string Name, bool IsGranted)[] changes) =>
        (await _directory.UpdatePermissionsAsync(userId, new UpdateUserPermissionsDto
        {
            Permissions = changes.Select(c => new UpdateUserPermissionDto { Name = c.Name, IsGranted = c.IsGranted }).ToList()
        })).Items;

    private Task<int> CountOverridesAsync(Guid userId) => WithUnitOfWorkAsync(async () =>
        (await _grants.GetListAsync(UserPermissionValueProvider.ProviderName, userId.ToString())).Count +
        (await _grants.GetListAsync(UserBlockPermissionValueProvider.ProviderName, userId.ToString())).Count);

    private Task<Guid> GetAdminIdAsync() => WithUnitOfWorkAsync(async () =>
        (await _userManager.FindByNameAsync("admin"))!.Id);

    private static ClaimsPrincipal PrincipalFor(Guid userId, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
            new Claim(AbpClaimTypes.Role, role),
        }, "Test"));
}
