using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Maintenance.Scopes;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Maintenance;

/* An admin blocks time (cleaning, renovation, ...) for a space, a whole floor or a whole building;
 * this decides which spaces that covers. Each kind of scope has its own IMaintenanceScopeStrategy. */
public class MaintenanceScopeResolver : DomainService
{
    private readonly Dictionary<MaintenanceScopeType, IMaintenanceScopeStrategy> _strategies;

    public MaintenanceScopeResolver(IEnumerable<IMaintenanceScopeStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.ScopeType);
    }

    /* For blocking time: a scope with no spaces is a mistake, so it is rejected. */
    public async Task<List<Guid>> GetSpaceIdsAsync(MaintenanceScopeType type, Guid scopeId)
    {
        var ids = await FindSpaceIdsAsync(type, scopeId);
        if (ids.Count == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceNotFound, message: "That scope has no spaces to block.").ForField("scopeId");
        return ids;
    }

    /* The same resolution without the check, for questions like "how many bookings are in this floor?". */
    public Task<List<Guid>> FindSpaceIdsAsync(MaintenanceScopeType type, Guid scopeId)
        => _strategies.TryGetValue(type, out var strategy)
            ? strategy.FindSpaceIdsAsync(scopeId)
            : throw new ArgumentOutOfRangeException(nameof(type), type, "No scope strategy is registered for this scope type.");
}
