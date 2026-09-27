using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

/* Turns floors into FloorDto. A floor DTO shows its building's name and how many spaces it has,
 * so mapping needs look-ups; keeping them here leaves FloorAppService with only the use cases. */
public class FloorDtoMapper : ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Space, Guid> _spaces;

    public FloorDtoMapper(IRepository<Building, Guid> buildings, IRepository<Space, Guid> spaces)
    {
        _buildings = buildings;
        _spaces = spaces;
    }

    public async Task<List<FloorDto>> MapListAsync(IEnumerable<Floor> floors)
    {
        /* Load each table once for the whole list instead of once per floor. */
        var buildings = (await _buildings.GetListAsync()).ToDictionary(b => b.Id);
        var spaceCounts = (await _spaces.GetListAsync()).GroupBy(s => s.FloorId).ToDictionary(g => g.Key, g => g.Count());
        return floors.Select(f => Map(f, buildings[f.BuildingId], spaceCounts.GetValueOrDefault(f.Id))).ToList();
    }

    public async Task<FloorDto> MapAsync(Floor floor)
        => Map(floor,
            await _buildings.GetAsync(floor.BuildingId),
            await _spaces.CountAsync(s => s.FloorId == floor.Id));

    private static FloorDto Map(Floor f, Building b, int spaceCount) => new()
    {
        Id = f.Id,
        BuildingId = f.BuildingId,
        BuildingName = b.Name,
        Name = f.Name,
        IsBookable = f.IsBookable,
        OpenHourOverride = f.OpenHourOverride,
        CloseHourOverride = f.CloseHourOverride,
        MinBookingMinutesOverride = f.MinBookingMinutesOverride,
        MaxBookingHoursOverride = f.MaxBookingHoursOverride,
        SpaceCount = spaceCount,
    };
}
