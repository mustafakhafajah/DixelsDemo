using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace Dixels.Portal.Identity;

/* Seeds a single non-admin demo account so the SPA's "regular user" OAuth
 * redirect path is actually testable locally (only the framework's default
 * admin account exists otherwise). Auto-discovered and run by ABP's
 * IDataSeeder alongside OpenIddictDataSeedContributor - no wiring needed. */
public class DemoUserDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string DemoUserName = "demo.user";
    public const string DemoUserEmail = "demo.user@dixels.io";
    public const string DemoUserPassword = "1q2w3E*";

    private readonly IdentityUserManager _userManager;

    public DemoUserDataSeedContributor(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (await _userManager.FindByNameAsync(DemoUserName) != null)
        {
            return;
        }

        var user = new IdentityUser(Guid.NewGuid(), DemoUserName, DemoUserEmail, context.TenantId);

        var result = await _userManager.CreateAsync(user, DemoUserPassword);
        if (!result.Succeeded)
        {
            throw new UserFriendlyException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
