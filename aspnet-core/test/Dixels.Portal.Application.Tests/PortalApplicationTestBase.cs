using Volo.Abp.Modularity;

namespace Dixels.Portal;

public abstract class PortalApplicationTestBase<TStartupModule> : PortalTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
