using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

/* Turns spaces into SpaceDto. A space DTO needs its building and floor (names plus resolved rules),
 * so mapping needs look-ups; keeping them here leaves SpaceAppService with only the use cases. */
public class SpaceDtoMapper : ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public SpaceDtoMapper(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors)
    {
        _buildings = buildings;
        _floors = floors;
    }

    public async Task<List<SpaceDto>> MapListAsync(IEnumerable<Space> spaces)
    {
        /* Load each table once for the whole list instead of once per space. */
        var buildings = (await _buildings.GetListAsync()).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync()).ToDictionary(f => f.Id);
        return spaces.Select(s => Map(s, buildings[s.BuildingId], floors.GetValueOrDefault(s.FloorId))).ToList();
    }

    public async Task<SpaceDto> MapAsync(Space space)
        => Map(space, await _buildings.GetAsync(space.BuildingId), await _floors.FindAsync(space.FloorId));

    private static SpaceDto Map(Space s, Building building, Floor? floor)
    {
        var ctx = new SpaceContext(s, floor, building);
        var c = ctx.Constraints;
        return new SpaceDto
        {
            Id = s.Id,
            Name = s.Name,
            Type = s.Type,
            Status = s.Status,
            BuildingId = s.BuildingId,
            BuildingName = building.Name,
            FloorId = s.FloorId,
            FloorName = floor?.Name ?? "",
            TimeZone = s.TimeZone,
            Capacity = s.Capacity,
            Note = s.Note,
            OpenHourOverride = s.OpenHourOverride,
            CloseHourOverride = s.CloseHourOverride,
            MinBookingMinutesOverride = s.MinBookingMinutesOverride,
            MaxBookingHoursOverride = s.MaxBookingHoursOverride,
            Constraints = new ResolvedConstraintsDto
            {
                OpenMinute = c.OpenMinute,
                CloseMinute = c.CloseMinute,
                MinBookingMinutes = c.MinBookingMinutes,
                MaxBookingHours = c.MaxBookingHours,
                Holidays = c.Holidays.ToList(),
            },
            CanCurrentUserBook = BookingManager.CanBook(ctx),
        };
    }
}
