using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.SpaceTypes;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Spaces;

/* Space rules: a unique name, a floor that belongs to the chosen building, an existing space type,
 * and overrides that only narrow the floor's rules. */
public class SpaceManager : DomainService
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<SpaceType, Guid> _types;

    public SpaceManager(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, IRepository<SpaceType, Guid> types)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _types = types;
    }

    public async Task<Space> CreateAsync(string name, Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        var trimmed = await CheckAsync(name, buildingId, floorId, typeId, overrides, null);
        var space = new Space(GuidGenerator.Create(), trimmed, buildingId, floorId, typeId);
        ApplyOverrides(space, overrides);
        return space;
    }

    public async Task UpdateAsync(Space space, string name, Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        space.Name = await CheckAsync(name, buildingId, floorId, typeId, overrides, space.Id);
        space.BuildingId = buildingId;
        space.FloorId = floorId;
        space.TypeId = typeId;
        ApplyOverrides(space, overrides);
    }

    /* Returns the trimmed name. excludeId is the space being edited, so its own name isn't a duplicate. */
    private async Task<string> CheckAsync(string? name, Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides o, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space a name.");
        var building = await _buildings.FindAsync(buildingId)
            ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building. Add one first if the list is empty.");
        var floor = await _floors.FindAsync(floorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new BusinessException(PortalDomainErrorCodes.InvalidFloor, $"Pick a floor that belongs to {building.Name}.");
        var lower = trimmed.ToLower();
        if (await _spaces.AnyAsync(s => s.Name.ToLower() == lower && s.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceDuplicateName, "Another space already uses that name.");
        if (!await _types.AnyAsync(t => t.Id == typeId))
            throw new BusinessException(PortalDomainErrorCodes.InvalidSpaceType, "Pick a space type. Add one on the Space types page if none fits.");

        EstateOverrideRules.EnsureOnlyNarrows("Space", "its floor", "its floor's",
            ConstraintResolver.ResolveBounds(building, floor),
            o.OpenHour, o.CloseHour, o.MinBookingMinutes, o.MaxBookingHours);
        return trimmed;
    }

    private static void ApplyOverrides(Space space, ConstraintOverrides o)
    {
        space.OpenHourOverride = o.OpenHour;
        space.CloseHourOverride = o.CloseHour;
        space.MinBookingMinutesOverride = o.MinBookingMinutes;
        space.MaxBookingHoursOverride = o.MaxBookingHours;
    }
}
