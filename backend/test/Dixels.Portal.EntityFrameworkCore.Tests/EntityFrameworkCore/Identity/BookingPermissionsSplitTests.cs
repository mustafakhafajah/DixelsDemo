using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Identity;
using Dixels.Portal.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Identity;

/* Bookings.ManageAll is split into one permission per action on other people's bookings. The tree must say what
 * each one needs, and whoever held ManageAll must keep every power it gave. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookingPermissionsSplitTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IPermissionDefinitionManager _definitions;
    private readonly IPermissionGrantRepository _grants;
    private readonly IGuidGenerator _guids;

    public BookingPermissionsSplitTests()
    {
        _definitions = GetRequiredService<IPermissionDefinitionManager>();
        _grants = GetRequiredService<IPermissionGrantRepository>();
        _guids = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Each_everyones_permission_sits_under_the_own_booking_permission_it_widens()
    {
        (await ParentOf(PortalPermissions.Bookings.ViewAll)).ShouldBe(PortalPermissions.Bookings.Default);
        (await ParentOf(PortalPermissions.Bookings.EditAll)).ShouldBe(PortalPermissions.Bookings.Edit);
        (await ParentOf(PortalPermissions.Bookings.DeleteAll)).ShouldBe(PortalPermissions.Bookings.Delete);
        (await ParentOf(PortalPermissions.Bookings.MultipleSpaces)).ShouldBe(PortalPermissions.Bookings.Create);
        (await _definitions.GetOrNullAsync(BookingPermissionsSplitDataSeedContributor.OldManageAll)).ShouldBeNull();
    }

    [Fact]
    public async Task Every_replacement_name_is_a_defined_permission()
    {
        foreach (var name in BookingPermissionsSplitDataSeedContributor.ReplacedBy)
            (await _definitions.GetOrNullAsync(name)).ShouldNotBeNull($"'{name}' is not a defined permission");
    }

    [Fact]
    public async Task Whoever_held_ManageAll_gets_all_four_and_loses_the_old_grant()
    {
        var role = "split-" + Guid.NewGuid().ToString("N")[..8];
        var userKey = Guid.NewGuid().ToString();
        await WithUnitOfWorkAsync(async () =>
        {
            await _grants.InsertAsync(new PermissionGrant(_guids.Create(), BookingPermissionsSplitDataSeedContributor.OldManageAll, RolePermissionValueProvider.ProviderName, role));
            await _grants.InsertAsync(new PermissionGrant(_guids.Create(), BookingPermissionsSplitDataSeedContributor.OldManageAll, UserPermissionValueProvider.ProviderName, userKey));
            /* Already holding one of the new ones must not make a duplicate. */
            await _grants.InsertAsync(new PermissionGrant(_guids.Create(), PortalPermissions.Bookings.ViewAll, RolePermissionValueProvider.ProviderName, role));
        });

        await SeedAsync();
        await SeedAsync();      // a second run changes nothing

        foreach (var (provider, key) in new[] { (RolePermissionValueProvider.ProviderName, role), (UserPermissionValueProvider.ProviderName, userKey) })
        {
            var names = await WithUnitOfWorkAsync(async () => (await _grants.GetListAsync(provider, key)).Select(g => g.Name).ToList());
            names.ShouldBe(BookingPermissionsSplitDataSeedContributor.ReplacedBy, ignoreOrder: true);
        }
    }

    private async Task<string?> ParentOf(string name) => (await _definitions.GetAsync(name)).Parent?.Name;

    private Task SeedAsync() => WithUnitOfWorkAsync(() =>
        GetRequiredService<BookingPermissionsSplitDataSeedContributor>().SeedAsync(new DataSeedContext()));
}
