using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance.Scopes;

/* A building covers every space on every floor in it. */
public class BuildingMaintenanceScopeStrategy : IMaintenanceScopeStrategy, ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;

    public BuildingMaintenanceScopeStrategy(IRepository<Space, Guid> spaces)
    {
        _spaces = spaces;
    }

    public MaintenanceScopeType ScopeType => MaintenanceScopeType.Building;

    public async Task<List<Guid>> FindSpaceIdsAsync(Guid scopeId)
        => (await _spaces.GetListAsync(s => s.BuildingId == scopeId)).Select(s => s.Id).ToList();
}
