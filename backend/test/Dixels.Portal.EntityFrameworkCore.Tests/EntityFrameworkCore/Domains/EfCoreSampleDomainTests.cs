using Dixels.Portal.Samples;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Domains;

[Collection(PortalTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<PortalEntityFrameworkCoreTestModule>
{

}
