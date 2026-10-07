using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace Dixels.Portal;

[DependsOn(
    typeof(PortalApplicationModule),
    typeof(PortalDomainTestModule)
)]
public class PortalApplicationTestModule : AbpModule
{
    /* No timers in tests: the guest clean-up is called directly where a test needs it. */
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
    }
}
