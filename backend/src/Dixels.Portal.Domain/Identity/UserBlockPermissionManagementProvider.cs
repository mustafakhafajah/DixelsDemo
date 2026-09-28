using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace Dixels.Portal.Identity;

/* Lets IPermissionManager write and remove the per-user blocks read by UserBlockPermissionValueProvider:
 * SetAsync(name, "UB", userId, true) stores a block, false removes it. Going through the manager keeps
 * ABP's permission grant cache in step. */
public class UserBlockPermissionManagementProvider : PermissionManagementProvider
{
    public override string Name => UserBlockPermissionValueProvider.ProviderName;

    public UserBlockPermissionManagementProvider(IPermissionGrantRepository permissionGrantRepository,
        IGuidGenerator guidGenerator, ICurrentTenant currentTenant)
        : base(permissionGrantRepository, guidGenerator, currentTenant)
    {
    }
}
