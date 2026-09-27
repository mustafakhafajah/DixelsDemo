using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

/* The rules a floor form must pass, shared by the create and update commands. */
public class FloorInputValidator : ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public FloorInputValidator(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors)
    {
        _buildings = buildings;
        _floors = floors;
    }

    public async Task<Building> GetBuildingAsync(Guid id)
        => await _buildings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building first.");

    /* Returns the trimmed name. excludeId is the floor being edited, so it doesn't clash with itself. */
    public async Task<string> ValidateAsync(Building building, CreateUpdateFloorDto input, Guid? excludeId)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Type a floor name or number.");
        if (await _floors.AnyAsync(f => f.BuildingId == building.Id && f.Name == name && f.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.FloorDuplicate, $"{building.Name} already has floor {name}.");
        EstateOverrideRules.EnsureOnlyNarrows("Floor", "the building", "the building's",
            ConstraintResolver.ResolveBounds(building, null),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
        return name;
    }
}
