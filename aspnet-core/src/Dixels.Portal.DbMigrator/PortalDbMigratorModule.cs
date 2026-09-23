using Dixels.Portal.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dixels.Portal.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(PortalEntityFrameworkCoreModule),
    typeof(PortalApplicationContractsModule)
    )]
public class PortalDbMigratorModule : AbpModule
{
}
