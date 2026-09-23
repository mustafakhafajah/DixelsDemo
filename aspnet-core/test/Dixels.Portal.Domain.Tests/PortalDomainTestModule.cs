using Volo.Abp.Modularity;

namespace Dixels.Portal;

[DependsOn(
    typeof(PortalDomainModule),
    typeof(PortalTestBaseModule)
)]
public class PortalDomainTestModule : AbpModule
{

}
