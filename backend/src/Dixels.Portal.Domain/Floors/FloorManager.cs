using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Floors;

/* Floor rules: a name unique within its building, and overrides that only narrow the building's rules. */
public class FloorManager : DomainService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public FloorManager(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors)
    {
        _buildings = buildings;
        _floors = floors;
    }

    public async Task<Floor> CreateAsync(Guid buildingId, string name, ConstraintOverrides overrides)
    {
        var building = await GetBuildingAsync(buildingId);
        var trimmed = await CheckAsync(building, name, overrides, null);
        var floor = new Floor(GuidGenerator.Create(), building.Id, trimmed);
        ApplyOverrides(floor, overrides);
        return floor;
    }

    /* A floor stays in its building: editing can change its name and rules, not move it. */
    public async Task UpdateAsync(Floor floor, string name, ConstraintOverrides overrides)
    {
        var building = await GetBuildingAsync(floor.BuildingId);
        floor.Name = await CheckAsync(building, name, overrides, floor.Id);
        ApplyOverrides(floor, overrides);
    }

    private async Task<Building> GetBuildingAsync(Guid id)
        => await _buildings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building first.");

    private async Task<string> CheckAsync(Building building, string? name, ConstraintOverrides o, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Type a floor name or number.");
        if (await _floors.AnyAsync(f => f.BuildingId == building.Id && f.Name == trimmed && f.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.FloorDuplicate, $"{building.Name} already has floor {trimmed}.");
        EstateOverrideRules.EnsureOnlyNarrows("Floor", "the building", "the building's",
            ConstraintResolver.ResolveBounds(building, null),
            o.OpenHour, o.CloseHour, o.MinBookingMinutes, o.MaxBookingHours);
        return trimmed;
    }

    private static void ApplyOverrides(Floor floor, ConstraintOverrides o)
    {
        floor.OpenHourOverride = o.OpenHour;
        floor.CloseHourOverride = o.CloseHour;
        floor.MinBookingMinutesOverride = o.MinBookingMinutes;
        floor.MaxBookingHoursOverride = o.MaxBookingHours;
    }
}
