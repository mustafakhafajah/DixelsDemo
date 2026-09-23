using Volo.Abp.Modularity;

namespace Dixels.Portal;

[DependsOn(
    typeof(PortalApplicationModule),
    typeof(PortalDomainTestModule)
)]
public class PortalApplicationTestModule : AbpModule
{

}
