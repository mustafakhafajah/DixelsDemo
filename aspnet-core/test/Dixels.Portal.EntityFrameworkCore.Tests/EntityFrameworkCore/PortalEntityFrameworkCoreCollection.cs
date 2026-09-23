using Xunit;

namespace Dixels.Portal.EntityFrameworkCore;

[CollectionDefinition(PortalTestConsts.CollectionDefinitionName)]
public class PortalEntityFrameworkCoreCollection : ICollectionFixture<PortalEntityFrameworkCoreFixture>
{

}
