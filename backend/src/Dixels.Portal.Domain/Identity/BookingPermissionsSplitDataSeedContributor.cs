using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace Dixels.Portal.Identity;

/* Portal.Bookings.ManageAll used to cover every action on other people's bookings. It is now split into one
 * permission per action (ViewAll, EditAll, DeleteAll) plus MultipleSpaces. Whoever held ManageAll - a role or a
 * user, granted directly - gets all four, so nobody loses anything on the day it ships, and the old grant is
 * removed. Runs with every DbMigrator seed; once nothing holds ManageAll it does nothing, so it is safe to run again.
 * Names are strings because PortalPermissions lives in Application.Contracts, which the Domain can't reference
 * (BookingPermissionsSplitTests fails if one of them is not a defined permission). */
public class BookingPermissionsSplitDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string OldManageAll = "Portal.Bookings.ManageAll";

    public static readonly string[] ReplacedBy =
    {
        "Portal.Bookings.ViewAll",
        "Portal.Bookings.EditAll",
        "Portal.Bookings.DeleteAll",
        "Portal.Bookings.MultipleSpaces",
    };

    private readonly IPermissionGrantRepository _grants;
    private readonly IGuidGenerator _guidGenerator;

    public ILogger<BookingPermissionsSplitDataSeedContributor> Logger { get; set; } = NullLogger<BookingPermissionsSplitDataSeedContributor>.Instance;

    public BookingPermissionsSplitDataSeedContributor(IPermissionGrantRepository grants, IGuidGenerator guidGenerator)
    {
        _grants = grants;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        var old = (await _grants.GetListAsync()).Where(g => g.Name == OldManageAll).ToList();
        foreach (var grant in old)
        {
            var held = (await _grants.GetListAsync(ReplacedBy, grant.ProviderName, grant.ProviderKey)).Select(g => g.Name).ToHashSet();
            foreach (var name in ReplacedBy.Where(n => !held.Contains(n)))
            {
                await _grants.InsertAsync(new PermissionGrant(_guidGenerator.Create(), name, grant.ProviderName, grant.ProviderKey, grant.TenantId));
            }
            await _grants.DeleteAsync(grant);
            Logger.LogInformation("Replaced {Old} with {New} for {Provider} {Key}.",
                OldManageAll, string.Join(", ", ReplacedBy), grant.ProviderName, grant.ProviderKey);
        }
    }
}
