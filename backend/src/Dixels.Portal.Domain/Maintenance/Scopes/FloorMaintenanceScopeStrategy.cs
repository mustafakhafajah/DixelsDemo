using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance.Scopes;

/* A floor covers every space on it. */
public class FloorMaintenanceScopeStrategy : IMaintenanceScopeStrategy, ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;

    public FloorMaintenanceScopeStrategy(IRepository<Space, Guid> spaces)
    {
        _spaces = spaces;
    }

    public MaintenanceScopeType ScopeType => MaintenanceScopeType.Floor;

    public async Task<List<Guid>> FindSpaceIdsAsync(Guid scopeId)
        => (await _spaces.GetListAsync(s => s.FloorId == scopeId)).Select(s => s.Id).ToList();
}
