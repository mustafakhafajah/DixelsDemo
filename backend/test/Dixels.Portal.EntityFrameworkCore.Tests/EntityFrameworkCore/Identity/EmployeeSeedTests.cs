using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Identity;
using Dixels.Portal.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Identity;

/* The DbMigrator seeds a working employee login: the employee role can see the estate and manage
 * its own bookings, and nothing an admin can. The test database is seeded the same way at start-up. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class EmployeeSeedTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly IPermissionManager _permissions;
    private readonly IPermissionDefinitionManager _definitions;

    public EmployeeSeedTests()
    {
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _permissions = GetRequiredService<IPermissionManager>();
        _definitions = GetRequiredService<IPermissionDefinitionManager>();
    }

    [Fact]
    public async Task Employee_user_exists_with_the_employee_role() => await WithUnitOfWorkAsync(async () =>
    {
        var user = await _users.FindByEmailAsync(RoleDataSeedContributor.EmployeeEmail);
        user.ShouldNotBeNull();
        user.EmailConfirmed.ShouldBeTrue();
        (await _users.CheckPasswordAsync(user, RoleDataSeedContributor.EmployeePassword)).ShouldBeTrue();
        (await _users.IsInRoleAsync(user, RoleDataSeedContributor.EmployeeRole)).ShouldBeTrue();

        var role = await _roles.FindByNameAsync(RoleDataSeedContributor.EmployeeRole);
        role!.IsDefault.ShouldBeTrue();
    });

    [Fact]
    public async Task Employee_role_holds_exactly_the_employee_permissions() => await WithUnitOfWorkAsync(async () =>
    {
        var granted = (await _permissions.GetAllForRoleAsync(RoleDataSeedContributor.EmployeeRole))
            .Where(p => p.IsGranted && p.Name.StartsWith(PortalPermissions.GroupName + "."))
            .Select(p => p.Name).OrderBy(n => n).ToArray();

        granted.ShouldBe(new[]
        {
            PortalPermissions.Bookings.Default, PortalPermissions.Bookings.Create,
            PortalPermissions.Bookings.Delete, PortalPermissions.Bookings.Edit,
            PortalPermissions.Buildings.Default, PortalPermissions.Floors.Default,
            PortalPermissions.Maintenance.Default, PortalPermissions.Spaces.Default,
            PortalPermissions.SpaceTypes.Default,
        }.OrderBy(n => n).ToArray());
    });

    [Fact]
    public async Task Every_seeded_permission_name_is_a_defined_permission()
    {
        foreach (var name in RoleDataSeedContributor.EmployeePermissions)
            (await _definitions.GetOrNullAsync(name)).ShouldNotBeNull($"'{name}' is not a defined permission");
    }

    [Fact]
    public async Task Seeding_again_changes_nothing()
    {
        await GetRequiredService<IDataSeeder>().SeedAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            (await _users.GetUsersInRoleAsync(RoleDataSeedContributor.EmployeeRole))
                .Count(u => u.Email == RoleDataSeedContributor.EmployeeEmail).ShouldBe(1);
        });
    }
}
