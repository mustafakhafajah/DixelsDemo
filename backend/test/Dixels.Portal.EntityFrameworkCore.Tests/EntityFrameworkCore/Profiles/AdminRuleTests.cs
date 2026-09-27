using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Profiles;
using Shouldly;
using Volo.Abp.PermissionManagement;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using IdentityUserManager = Volo.Abp.Identity.IdentityUserManager;

namespace Dixels.Portal.EntityFrameworkCore.Profiles;

/* "Admin" means holding Bookings.ManageAll, whether through the admin role or granted directly.
 * The user list and the rest of the app must agree on that one rule. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class AdminRuleTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IProfileLookupAppService _profiles;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionManager _permissions;

    public AdminRuleTests()
    {
        _profiles = GetRequiredService<IProfileLookupAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _permissions = GetRequiredService<IPermissionManager>();
    }

    [Fact]
    public async Task Admin_role_counts_as_admin_and_a_plain_user_does_not()
    {
        var plain = await CreateUserAsync("plain.user");

        var users = (await _profiles.GetUsersAsync()).Items;

        users.Single(u => u.Name == "admin").IsAdmin.ShouldBeTrue();
        users.Single(u => u.Id == plain.Id).IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task The_permission_decides_not_the_role_name()
    {
        var granted = await CreateUserAsync("granted.user");
        await WithUnitOfWorkAsync(() =>
            _permissions.SetForUserAsync(granted.Id, PortalAdminRule.Permission, true));

        var users = (await _profiles.GetUsersAsync()).Items;

        users.Single(u => u.Id == granted.Id).IsAdmin.ShouldBeTrue();
    }

    private Task<IdentityUser> CreateUserAsync(string userName) => WithUnitOfWorkAsync(async () =>
    {
        var user = new IdentityUser(Guid.NewGuid(), userName, $"{userName}@dixels.io");
        (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return user;
    });
}
