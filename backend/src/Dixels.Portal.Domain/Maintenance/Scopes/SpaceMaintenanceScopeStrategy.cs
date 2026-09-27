using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance.Scopes;

/* A single space covers just itself (or nothing, if it doesn't exist). */
public class SpaceMaintenanceScopeStrategy : IMaintenanceScopeStrategy, ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceMaintenanceScopeStrategy(IRepository<Space, Guid> spaces)
    {
        _spaces = spaces;
    }

    public MaintenanceScopeType ScopeType => MaintenanceScopeType.Space;

    public async Task<List<Guid>> FindSpaceIdsAsync(Guid scopeId)
        => await _spaces.AnyAsync(s => s.Id == scopeId) ? new List<Guid> { scopeId } : new List<Guid>();
}
