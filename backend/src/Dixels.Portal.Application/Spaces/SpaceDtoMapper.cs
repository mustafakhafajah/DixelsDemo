using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.SpaceTypes;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

/* Turns spaces into SpaceDto. A space DTO needs its building, floor and type (names, time zone, resolved rules),
 * so mapping needs look-ups; keeping them here leaves SpaceAppService with only the use cases. */
public class SpaceDtoMapper : ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<SpaceType, Guid> _types;

    public SpaceDtoMapper(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors, IRepository<SpaceType, Guid> types)
    {
        _buildings = buildings;
        _floors = floors;
        _types = types;
    }

    public async Task<List<SpaceDto>> MapListAsync(IEnumerable<Space> spaces)
    {
        /* Load each table once for the whole list instead of once per space. */
        var buildings = (await _buildings.GetListAsync()).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync()).ToDictionary(f => f.Id);
        var types = (await _types.GetListAsync()).ToDictionary(t => t.Id, t => t.Name);
        return spaces.Select(s => Map(s, buildings[s.BuildingId], floors.GetValueOrDefault(s.FloorId), types.GetValueOrDefault(s.TypeId)))
            .ToList();
    }

    public async Task<SpaceDto> MapAsync(Space space)
        => Map(space, await _buildings.GetAsync(space.BuildingId), await _floors.FindAsync(space.FloorId),
            (await _types.FindAsync(space.TypeId))?.Name);

    private static SpaceDto Map(Space s, Building building, Floor? floor, string? typeName)
    {
        var ctx = new SpaceContext(s, floor, building);
        var c = ctx.Constraints;
        return new SpaceDto
        {
            Id = s.Id,
            Name = s.Name,
            TypeId = s.TypeId,
            TypeName = typeName ?? "Unknown type",
            IsBookable = s.IsBookable,
            BuildingId = s.BuildingId,
            BuildingName = building.Name,
            FloorId = s.FloorId,
            FloorName = floor?.Name ?? "",
            TimeZone = building.TimeZone,
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
            NotBookableReason = BookingManager.FindNotBookableReason(ctx),
        };
    }
}
