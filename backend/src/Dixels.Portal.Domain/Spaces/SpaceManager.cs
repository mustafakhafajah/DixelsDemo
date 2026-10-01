using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.SpaceTypes;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

/* Space rules: a name unique within each language, a floor that belongs to the chosen building, an existing
 * space type, and overrides that only narrow the floor's rules. */
public class SpaceManager : PortalDomainService
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

    /* The name and note are saved in the language the admin is using. */
    public async Task<Space> CreateAsync(string name, string? note, Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        var language = PortalLanguages.Current;
        var trimmed = await CheckAsync(name, language, buildingId, floorId, typeId, overrides, null);
        var space = new Space(GuidGenerator.Create(), language, trimmed, buildingId, floorId, typeId, CleanNote(note));
        ApplyOverrides(space, overrides);
        return space;
    }

    /* Changes the name and note in the admin's language only. Saving the fallback text unchanged adds no translation. */
    public async Task UpdateAsync(Space space, string name, string? note, Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        var language = PortalLanguages.Current;
        var trimmed = name?.Trim() ?? "";
        var cleanNote = CleanNote(note);
        var keepsFallback = space.FindTranslation(language) == null
            && trimmed == space.GetName(language) && cleanNote == space.GetNote(language);
        trimmed = await CheckAsync(trimmed, language, buildingId, floorId, typeId, overrides, space.Id, checkName: !keepsFallback);
        if (!keepsFallback) space.SetText(language, trimmed, cleanNote);
        space.BuildingId = buildingId;
        space.FloorId = floorId;
        space.TypeId = typeId;
        ApplyOverrides(space, overrides);
    }

    private static string? CleanNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /* Returns the trimmed name. excludeId is the space being edited, so its own name isn't a duplicate. */
    private async Task<string> CheckAsync(string? name, string language, Guid buildingId, Guid floorId, Guid typeId,
        ConstraintOverrides o, Guid? excludeId, bool checkName = true)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceNameMissing"]).ForField("name");
        var building = await _buildings.FindAsync(buildingId)
            ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceBuildingMissing"]).ForField("buildingId");
        var floor = await _floors.FindAsync(floorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidFloor, message: L["Error:SpaceFloorInvalid", building.GetName()]).ForField("floorId");
        var lower = trimmed.ToLower();
        if (checkName && await _spaces.AnyAsync(s => s.Id != excludeId && s.Translations.Any(t => t.Language == language && t.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceDuplicateName, message: L["Error:SpaceDuplicate"]).ForField("name");
        if (!await _types.AnyAsync(t => t.Id == typeId))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidSpaceType, message: L["Error:SpaceTypeMissing"]).ForField("typeId");

        EstateOverrideRules.EnsureOnlyNarrows(L, EstateOverrideRules.SpaceLevel,
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
