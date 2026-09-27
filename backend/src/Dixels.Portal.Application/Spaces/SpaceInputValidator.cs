using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.SpaceTypes;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

/* The rules a space form must pass, shared by the create and update commands. */
public class SpaceInputValidator : ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<SpaceType, Guid> _types;

    public SpaceInputValidator(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, IRepository<SpaceType, Guid> types)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _types = types;
    }

    /* excludeId is the space being edited, so its own name doesn't count as a duplicate. */
    public async Task<(string Name, Building Building, Floor Floor)> ValidateAsync(CreateUpdateSpaceDto input, Guid? excludeId)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space a name.");
        var building = await _buildings.FindAsync(input.BuildingId)
            ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building. Add one first if the list is empty.");
        var floor = await _floors.FindAsync(input.FloorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new BusinessException(PortalDomainErrorCodes.InvalidFloor, $"Pick a floor that belongs to {building.Name}.");
        var lower = name.ToLower();
        if (await _spaces.AnyAsync(s => s.Name.ToLower() == lower && s.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceDuplicateName, "Another space already uses that name.");
        if (!await _types.AnyAsync(t => t.Id == input.TypeId))
            throw new BusinessException(PortalDomainErrorCodes.InvalidSpaceType, "Pick a space type. Add one on the Space types page if none fits.");

        EstateOverrideRules.EnsureOnlyNarrows("Space", "its floor", "its floor's",
            ConstraintResolver.ResolveBounds(building, floor),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
        return (name, building, floor);
    }
}
