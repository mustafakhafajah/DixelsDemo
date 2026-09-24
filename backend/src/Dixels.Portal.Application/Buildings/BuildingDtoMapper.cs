using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

/* Turns buildings into BuildingDto. A building DTO shows how many floors and spaces it has,
 * so mapping needs look-ups; keeping them here leaves BuildingAppService with only the use cases. */
public class BuildingDtoMapper : ITransientDependency
{
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public BuildingDtoMapper(IRepository<Floor, Guid> floors, IRepository<Space, Guid> spaces)
    {
        _floors = floors;
        _spaces = spaces;
    }

    public async Task<List<BuildingDto>> MapListAsync(IEnumerable<Building> buildings)
    {
        /* Load each table once for the whole list instead of counting per building. */
        var floorCounts = (await _floors.GetListAsync()).GroupBy(f => f.BuildingId).ToDictionary(g => g.Key, g => g.Count());
        var spaceCounts = (await _spaces.GetListAsync()).GroupBy(s => s.BuildingId).ToDictionary(g => g.Key, g => g.Count());
        return buildings.Select(b => Map(b, floorCounts.GetValueOrDefault(b.Id), spaceCounts.GetValueOrDefault(b.Id))).ToList();
    }

    public async Task<BuildingDto> MapAsync(Building building)
        => Map(building,
            await _floors.CountAsync(f => f.BuildingId == building.Id),
            await _spaces.CountAsync(s => s.BuildingId == building.Id));

    private static BuildingDto Map(Building b, int floorCount, int spaceCount) => new()
    {
        Id = b.Id,
        Name = b.Name,
        TimeZone = b.TimeZone,
        Status = b.Status,
        OpenHour = b.OpenHour,
        CloseHour = b.CloseHour,
        MinBookingMinutes = b.MinBookingMinutes,
        MaxBookingHours = b.MaxBookingHours,
        Holidays = b.Holidays.ToList(),
        FloorCount = floorCount,
        SpaceCount = spaceCount,
    };
}
