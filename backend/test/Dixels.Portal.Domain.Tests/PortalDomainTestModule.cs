using Dixels.Portal.BlobStoring;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dixels.Portal;

[DependsOn(
    typeof(PortalDomainModule),
    typeof(PortalTestBaseModule)
)]
public class PortalDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        /* Post-configured, so it also replaces the Web module's file system in the web tests: no test writes to disk. */
        PostConfigure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureAll((_, container) => container.ProviderType = typeof(InMemoryBlobProvider));
        });
    }
}
