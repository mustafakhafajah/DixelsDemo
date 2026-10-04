using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Users;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Users;

/* Every user-directory filter runs on the server through ABP's user repository, and the total counts the
 * filtered users, not the page. Each test makes its own users under a unique tag, so the search narrows
 * the directory to them. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class UserDirectoryTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IUserDirectoryAppService _directory;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;

    public UserDirectoryTests()
    {
        _directory = GetRequiredService<IUserDirectoryAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
    }

    [Fact]
    public async Task Search_role_active_and_locked_are_applied_on_the_server()
    {
        var p = await CreatePeopleAsync();

        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag }))
            .ShouldBe(new[] { p.Active, p.Inactive, p.Locked }, ignoreOrder: true);
        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag, RoleId = p.CrewRoleId }))
            .ShouldBe(new[] { p.Active });
        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag, IsActive = false }))
            .ShouldBe(new[] { p.Inactive });
        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag, IsActive = true }))
            .ShouldBe(new[] { p.Active, p.Locked }, ignoreOrder: true);
        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag, IsLocked = true }))
            .ShouldBe(new[] { p.Locked });
        (await UserNamesAsync(new GetUserDirectoryInput { Filter = p.Tag, IsLocked = false }))
            .ShouldBe(new[] { p.Active, p.Inactive }, ignoreOrder: true);
    }

    [Fact]
    public async Task Pages_come_from_the_server_with_the_filtered_total_and_role_names()
    {
        var p = await CreatePeopleAsync();

        var first = await _directory.GetListAsync(new GetUserDirectoryInput { Filter = p.Tag, MaxResultCount = 2 });
        var second = await _directory.GetListAsync(new GetUserDirectoryInput { Filter = p.Tag, SkipCount = 2, MaxResultCount = 2 });

        first.TotalCount.ShouldBe(3);
        first.Items.Count.ShouldBe(2);
        second.Items.Count.ShouldBe(1);
        first.Items.Concat(second.Items).Select(u => u.Id).Distinct().Count().ShouldBe(3);

        var all = first.Items.Concat(second.Items).ToList();
        all.Single(u => u.UserName == p.Active).Roles.ShouldBe(new[] { $"{p.Tag}-crew" });
        all.Single(u => u.UserName == p.Locked).IsLocked.ShouldBeTrue();
        all.Single(u => u.UserName == p.Locked).LockoutEnd.ShouldNotBeNull();
        all.Single(u => u.UserName == p.Inactive).IsActive.ShouldBeFalse();
    }

    private async Task<string[]> UserNamesAsync(GetUserDirectoryInput input)
        => (await _directory.GetListAsync(input)).Items.Select(u => u.UserName).ToArray();

    private record People(string Tag, Guid CrewRoleId, string Active, string Inactive, string Locked);

    /* Active (in the "crew" role), inactive, and locked out until tomorrow. */
    private Task<People> CreatePeopleAsync() => WithUnitOfWorkAsync(async () =>
    {
        var tag = "dir" + Guid.NewGuid().ToString("N")[..8];
        var crew = new IdentityRole(Guid.NewGuid(), $"{tag}-crew");
        (await _roles.CreateAsync(crew)).Succeeded.ShouldBeTrue();

        async Task<IdentityUser> Create(string name)
        {
            var user = new IdentityUser(Guid.NewGuid(), $"{tag}.{name}", $"{tag}.{name}@dixels.io");
            (await _users.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user;
        }

        var active = await Create("active");
        (await _users.AddToRoleAsync(active, crew.Name)).Succeeded.ShouldBeTrue();

        var inactive = await Create("inactive");
        inactive.SetIsActive(false);
        (await _users.UpdateAsync(inactive)).Succeeded.ShouldBeTrue();

        var locked = await Create("locked");
        (await _users.SetLockoutEnabledAsync(locked, true)).Succeeded.ShouldBeTrue();
        (await _users.SetLockoutEndDateAsync(locked, DateTimeOffset.UtcNow.AddDays(1))).Succeeded.ShouldBeTrue();

        return new People(tag, crew.Id, active.UserName, inactive.UserName, locked.UserName);
    });
}
