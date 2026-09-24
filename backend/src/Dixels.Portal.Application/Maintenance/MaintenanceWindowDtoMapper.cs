using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Maintenance;

/* Turns maintenance windows into MaintenanceWindowDto, including the display label of the original
 * scope ("HQ North · Floor 3"); that label is presentation, so it lives here and not in the domain. */
public class MaintenanceWindowDtoMapper : ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IClock _clock;

    public MaintenanceWindowDtoMapper(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, IClock clock)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _clock = clock;
    }

    public async Task<List<MaintenanceWindowDto>> MapListAsync(IReadOnlyCollection<MaintenanceWindow> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(m => m.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name);
        /* A series across a building shares one scope, so each distinct scope is labelled once. */
        var labels = new Dictionary<(MaintenanceScopeType, Guid), string>();
        foreach (var key in list.Select(m => (m.ScopeType, m.ScopeId)).Distinct())
            labels[key] = await ScopeLabelAsync(key.ScopeType, key.ScopeId);
        var now = _clock.Now;
        return list.Select(m => new MaintenanceWindowDto
        {
            Id = m.Id,
            SpaceId = m.SpaceId,
            SpaceName = spaces.GetValueOrDefault(m.SpaceId, "Unknown space"),
            StartUtc = m.StartUtc,
            EndUtc = m.EndUtc,
            Note = m.Note,
            SeriesId = m.SeriesId,
            ScopeType = m.ScopeType,
            ScopeId = m.ScopeId,
            ScopeLabel = labels[(m.ScopeType, m.ScopeId)],
            Status = m.Status,
            Lifecycle = m.GetLifecycle(now),
            CreationTime = m.CreationTime,
            CreatorId = m.CreatorId,
        }).ToList();
    }

    public async Task<MaintenanceWindowDto> MapAsync(MaintenanceWindow window) => (await MapListAsync(new[] { window }))[0];

    private async Task<string> ScopeLabelAsync(MaintenanceScopeType type, Guid scopeId)
    {
        switch (type)
        {
            case MaintenanceScopeType.Space:
                return (await _spaces.FindAsync(scopeId))?.Name ?? "a space";
            case MaintenanceScopeType.Floor:
                var f = await _floors.FindAsync(scopeId);
                if (f == null) return "a floor";
                var fb = await _buildings.FindAsync(f.BuildingId);
                return $"{fb?.Name} · Floor {f.Name}";
            default:
                return (await _buildings.FindAsync(scopeId))?.Name ?? "a building";
        }
    }
}
