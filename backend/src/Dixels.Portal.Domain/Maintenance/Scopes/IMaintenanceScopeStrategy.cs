using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dixels.Portal.Maintenance.Scopes;

/* How one kind of scope (space, floor, building...) turns into the spaces it covers.
 * A new kind of scope is a new class implementing this; MaintenanceScopeResolver doesn't change. */
public interface IMaintenanceScopeStrategy
{
    MaintenanceScopeType ScopeType { get; }

    Task<List<Guid>> FindSpaceIdsAsync(Guid scopeId);
}
