using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Identity;
using Dixels.Portal.Users;
using Shouldly;
using Volo.Abp;
using Xunit;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
using IdentityRoleManager = Volo.Abp.Identity.IdentityRoleManager;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using IdentityUserManager = Volo.Abp.Identity.IdentityUserManager;

namespace Dixels.Portal.EntityFrameworkCore.Users;

/* The admin user directory (view only). The test database is seeded with the "admin" user (admin role) and
 * employee@dixels.io (employee role); extra users and roles are made straight through Identity. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class UserDirectoryTests : PortalEntityFrameworkCoreTestBase
{
    private const string Password = "1q2w3E*";

    private readonly IUserDirectoryAppService _directory;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;

    public UserDirectoryTests()
    {
        _directory = GetRequiredService<IUserDirectoryAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
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

        (await ListAsync(new UserDirectoryListInput { Filter = "QUINN" })).ShouldBe(new[] { zed });
        (await ListAsync(new UserDirectoryListInput { Filter = "ZED@DIXELS" })).ShouldBe(new[] { zed });
        (await ListAsync(new UserDirectoryListInput { Role = "admin" })).Count.ShouldBe(2);
        (await ListAsync(new UserDirectoryListInput { Role = "employee", Filter = "amy" })).ShouldBeEmpty();

        var until = DateTimeOffset.UtcNow.AddDays(3);
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(zed);
            user.SetIsActive(false);
            (await _userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();

            var admin = await _userManager.GetByIdAsync(amy);
            (await _userManager.SetLockoutEnabledAsync(admin, true)).Succeeded.ShouldBeTrue();
            (await _userManager.SetLockoutEndDateAsync(admin, until)).Succeeded.ShouldBeTrue();
        });

        (await ListAsync(new UserDirectoryListInput { IsActive = false })).ShouldBe(new[] { zed });
        (await ListAsync(new UserDirectoryListInput { IsLocked = true })).ShouldBe(new[] { amy });
        (await ListAsync(new UserDirectoryListInput { IsLocked = false, IsActive = true })).Count.ShouldBe(2);

        var locked = (await _directory.GetListAsync(new UserDirectoryListInput { IsLocked = true })).Items.Single();
        locked.IsLocked.ShouldBeTrue();
        locked.LockoutEnd!.Value.ShouldBe(until.UtcDateTime, TimeSpan.FromSeconds(1));
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
    public async Task Lists_the_roles_admin_first()
    {
        var roles = (await _directory.GetRolesAsync()).Items;

        roles.Select(r => r.Name).ShouldBe(new[] { UserDirectoryRoles.Admin, UserDirectoryRoles.Employee });
        roles.Select(r => r.DisplayName).ShouldBe(new[] { "Admin", "Employee" });
        roles.Select(r => r.IsAdmin).ShouldBe(new[] { true, false });
    }

    [Fact]
    public async Task Any_existing_role_is_listed_and_can_be_filtered_on()
    {
        await CreateRoleAsync("front_desk");
        var roles = (await _directory.GetRolesAsync()).Items;
        roles.Select(r => r.Name).ShouldBe(new[] { UserDirectoryRoles.Admin, UserDirectoryRoles.Employee, "front_desk" });
        roles.Single(r => r.Name == "front_desk").DisplayName.ShouldBe("Front desk");
        roles.Single(r => r.Name == "front_desk").IsAdmin.ShouldBeFalse();

        var desk = await CreateAsync("Desk Person", "desk@dixels.io", "front_desk");
        (await ListAsync(new UserDirectoryListInput { Role = "FRONT_DESK" })).ShouldBe(new[] { desk });
        var listed = (await _directory.GetListAsync(new UserDirectoryListInput { Filter = "desk@" })).Items.Single();
        listed.Role.ShouldBe("front_desk");
        listed.Roles.ShouldBe(new[] { "front_desk" });
        (await ListAsync(new UserDirectoryListInput { Role = "employee", Filter = "desk" })).ShouldBeEmpty();
    }

    [Fact]
    public async Task Admin_is_the_main_role()
    {
        await CreateRoleAsync("front_desk");
        var user = await CreateAsync("Two Hats", "two.hats@dixels.io", "front_desk");
        await WithUnitOfWorkAsync(async () =>
            await _userManager.AddToRoleAsync(await _userManager.GetByIdAsync(user), UserDirectoryRoles.Admin));

        var listed = (await _directory.GetListAsync(new UserDirectoryListInput { Filter = "two.hats" })).Items.Single();
        listed.Role.ShouldBe(UserDirectoryRoles.Admin);
        listed.Roles.ShouldBe(new[] { UserDirectoryRoles.Admin, "front_desk" });
    }

    [Fact]
    public async Task Filtering_on_an_unknown_role_is_rejected()
    {
        var error = await Should.ThrowAsync<UserFriendlyException>(() => ListAsync(new UserDirectoryListInput { Role = "superuser" }));

        error.Code.ShouldBe(PortalDomainErrorCodes.UserInvalidRole);
        error.Message.ShouldBe("That role doesn't exist.");
    }

    /* A confirmed user named `name`, signing in with their email, in `role`. */
    private Task<Guid> CreateAsync(string name, string email, string role) => WithUnitOfWorkAsync(async () =>
    {
        var user = new IdentityUser(Guid.NewGuid(), email, email) { Name = name };
        user.SetEmailConfirmed(true);
        (await _userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        (await _userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        return user.Id;
    });

    private Task CreateRoleAsync(string name) => WithUnitOfWorkAsync(async () =>
        (await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), name))).Succeeded.ShouldBeTrue());

    private async Task<List<Guid>> ListAsync(UserDirectoryListInput input) =>
        (await _directory.GetListAsync(input)).Items.Select(u => u.Id).ToList();
}
