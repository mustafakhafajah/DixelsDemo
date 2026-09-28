using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace Dixels.Portal.Identity;

/* ABP permissions only add up: a user holds a permission when any of their roles (or the user)
 * is granted it. To take one role permission away from a single user we store a "block" in ABP's
 * own permission grant table under the provider name "UB", keyed by the user's id, and answer
 * Prohibited for it here. Prohibited beats every Granted, so the permission is denied everywhere
 * ([Authorize], IPermissionChecker, the grantedPolicies the SPA reads).
 *
 * Must run before the role provider: ABP's multi-permission check keeps the first answer that
 * is not Undefined, so PortalDomainModule inserts this provider at the front of the list. */
public class UserBlockPermissionValueProvider : PermissionValueProvider
{
    public const string ProviderName = "UB";

    public override string Name => ProviderName;

    public UserBlockPermissionValueProvider(IPermissionStore permissionStore)
        : base(permissionStore)
    {
    }

    public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
    {
        var userId = context.Principal?.FindFirst(AbpClaimTypes.UserId)?.Value;
        if (userId == null)
        {
            return PermissionGrantResult.Undefined;
        }

        return await PermissionStore.IsGrantedAsync(context.Permission.Name, Name, userId)
            ? PermissionGrantResult.Prohibited
            : PermissionGrantResult.Undefined;
    }

    public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var names = context.Permissions.Select(p => p.Name).Distinct().ToArray();
        Check.NotNullOrEmpty(names, nameof(names));

        var result = new MultiplePermissionGrantResult(names);
        var userId = context.Principal?.FindFirst(AbpClaimTypes.UserId)?.Value;
        if (userId == null)
        {
            return result;
        }

        /* The store says "Granted" when a block row exists; for us that means Prohibited. */
        var blocks = await PermissionStore.IsGrantedAsync(names, Name, userId);
        foreach (var (name, found) in blocks.Result)
        {
            result.Result[name] = found == PermissionGrantResult.Granted
                ? PermissionGrantResult.Prohibited
                : PermissionGrantResult.Undefined;
        }

        return result;
    }
}
