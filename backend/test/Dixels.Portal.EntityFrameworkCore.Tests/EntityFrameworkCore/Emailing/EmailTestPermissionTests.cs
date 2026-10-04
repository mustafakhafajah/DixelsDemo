using System.Threading.Tasks;
using Dixels.Portal.Identity;
using Dixels.Portal.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Emailing;

/* Sending a test email shows the mail server's replies and settings, so it is for admins only: the admin role gets it
 * from ABP's seeding (every defined permission), the employee role does not. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class EmailTestPermissionTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IPermissionManager _permissions;

    public EmailTestPermissionTests()
    {
        _permissions = GetRequiredService<IPermissionManager>();
    }

    [Fact]
    public async Task Send_test_is_a_defined_permission() =>
        (await GetRequiredService<IPermissionDefinitionManager>().GetOrNullAsync(PortalPermissions.Emailing.SendTest)).ShouldNotBeNull();

    [Fact]
    public async Task Admins_have_it_and_employees_do_not() => await WithUnitOfWorkAsync(async () =>
    {
        (await _permissions.GetForRoleAsync("admin", PortalPermissions.Emailing.SendTest)).IsGranted.ShouldBeTrue();
        (await _permissions.GetForRoleAsync(RoleDataSeedContributor.EmployeeRole, PortalPermissions.Emailing.SendTest)).IsGranted.ShouldBeFalse();
    });
}
