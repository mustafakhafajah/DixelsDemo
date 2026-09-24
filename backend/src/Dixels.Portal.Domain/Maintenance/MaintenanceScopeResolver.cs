using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Maintenance;

/* An admin schedules cleaning for a space, a whole floor or a whole building;
 * this decides which spaces that covers. */
public class MaintenanceScopeResolver : DomainService
{
    private readonly IRepository<Space, Guid> _spaces;

    public MaintenanceScopeResolver(IRepository<Space, Guid> spaces)
    {
        _spaces = spaces;
    }

    public async Task<List<Guid>> GetSpaceIdsAsync(MaintenanceScopeType type, Guid scopeId)
    {
        var ids = type switch
        {
            MaintenanceScopeType.Space => await _spaces.AnyAsync(s => s.Id == scopeId) ? new List<Guid> { scopeId } : new(),
            MaintenanceScopeType.Floor => (await _spaces.GetListAsync(s => s.FloorId == scopeId)).Select(s => s.Id).ToList(),
            _ => (await _spaces.GetListAsync(s => s.BuildingId == scopeId)).Select(s => s.Id).ToList(),
        };
        if (ids.Count == 0)
            throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "That scope has no spaces to clean.");
        return ids;
    }
}
