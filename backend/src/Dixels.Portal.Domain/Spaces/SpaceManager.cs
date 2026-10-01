using System;
using System.Collections.Generic;
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

/* Space rules: an English name, optional names (and notes) in other languages, each name unique within its
 * language; a floor that belongs to the chosen building, an existing space type, and overrides that only
 * narrow the floor's rules. */
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

    /* name and note are the English ones. */
    public async Task<Space> CreateAsync(string name, string? note, IEnumerable<NameTranslation>? translations,
        Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        var (english, extras) = await CheckAsync(name, translations, buildingId, floorId, typeId, overrides, null);
        var space = new Space(GuidGenerator.Create(), english, buildingId, floorId, typeId, CleanNote(note));
        foreach (var t in extras) space.SetText(t.Language, t.Name, t.Note);
        ApplyOverrides(space, overrides);
        return space;
    }

    /* translations is the full set of extra languages: one left out is removed. */
    public async Task UpdateAsync(Space space, string name, string? note, IEnumerable<NameTranslation>? translations,
        Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides overrides)
    {
        var (english, extras) = await CheckAsync(name, translations, buildingId, floorId, typeId, overrides, space.Id);
        space.SetText(PortalLanguages.Default, english, CleanNote(note));
        foreach (var t in extras) space.SetText(t.Language, t.Name, t.Note);
        space.RemoveTranslationsExcept(extras.Select(t => t.Language));
        space.BuildingId = buildingId;
        space.FloorId = floorId;
        space.TypeId = typeId;
        ApplyOverrides(space, overrides);
    }

    private static string? CleanNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private async Task<(string English, List<NameTranslation> Extras)> CheckAsync(string? name, IEnumerable<NameTranslation>? translations,
        Guid buildingId, Guid floorId, Guid typeId, ConstraintOverrides o, Guid? excludeId)
    {
        var english = await CheckNameAsync(PortalLanguages.Default, name, excludeId, "name");
        var extras = TranslationRules.Clean(L, translations, SpaceConsts.MaxNameLength, SpaceConsts.MaxNoteLength);
        foreach (var t in extras) await CheckNameAsync(t.Language, t.Name, excludeId, $"translations.{t.Language}");

        var building = await _buildings.FindAsync(buildingId)
            ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceBuildingMissing"]).ForField("buildingId");
        var floor = await _floors.FindAsync(floorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidFloor, message: L["Error:SpaceFloorInvalid", building.GetName()]).ForField("floorId");
        if (!await _types.AnyAsync(t => t.Id == typeId))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidSpaceType, message: L["Error:SpaceTypeMissing"]).ForField("typeId");

        EstateOverrideRules.EnsureOnlyNarrows(L, EstateOverrideRules.SpaceLevel,
            ConstraintResolver.ResolveBounds(building, floor),
            o.OpenHour, o.CloseHour, o.MinBookingMinutes, o.MaxBookingHours);
        return (english, extras);
    }

    /* Returns the trimmed name. excludeId is the space being edited, so its own name isn't a duplicate. */
    private async Task<string> CheckNameAsync(string language, string? name, Guid? excludeId, string field)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceNameMissing"]).ForField(field);
        var lower = trimmed.ToLower();
        if (await _spaces.AnyAsync(s => s.Id != excludeId && s.Translations.Any(t => t.Language == language && t.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceDuplicateName, message: L["Error:SpaceDuplicate"]).ForField(field);
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
