using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Maintenance.Scopes;
using Shouldly;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Maintenance;

/* Adding a scope type (e.g. Campus) means adding a strategy class. This fails if one is forgotten,
 * or if two strategies claim the same scope type. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class ScopeStrategyTests : PortalEntityFrameworkCoreTestBase
{
    [Fact]
    public void Every_scope_type_has_exactly_one_strategy()
    {
        var strategies = GetRequiredService<IEnumerable<IMaintenanceScopeStrategy>>().ToList();

        foreach (var type in Enum.GetValues<MaintenanceScopeType>())
            strategies.Count(s => s.ScopeType == type).ShouldBe(1, $"{type} needs exactly one IMaintenanceScopeStrategy");
    }
}
