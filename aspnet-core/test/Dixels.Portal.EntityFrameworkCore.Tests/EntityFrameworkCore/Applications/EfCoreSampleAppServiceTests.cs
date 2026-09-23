using Dixels.Portal.Samples;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Applications;

[Collection(PortalTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<PortalEntityFrameworkCoreTestModule>
{

}
