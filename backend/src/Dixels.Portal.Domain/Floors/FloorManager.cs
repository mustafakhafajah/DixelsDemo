using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

/* Floor rules: an English name, optional names in other languages, each unique within its building and
 * language; and overrides that only narrow the building's rules. */
public class FloorManager : PortalDomainService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public FloorManager(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors)
    {
        _buildings = buildings;
        _floors = floors;
    }

    public async Task<Floor> CreateAsync(Guid buildingId, string name, IEnumerable<NameTranslation>? translations, ConstraintOverrides overrides)
    {
        var building = await GetBuildingAsync(buildingId);
        var (english, extras) = await CheckAsync(building, name, translations, overrides, null);
        var floor = new Floor(GuidGenerator.Create(), building.Id, english);
        foreach (var t in extras) floor.SetName(t.Language, t.Name);
        ApplyOverrides(floor, overrides);
        return floor;
    }

    /* A floor stays in its building: editing can change its names and rules, not move it.
     * translations is the full set of extra languages: one left out is removed. */
    public async Task UpdateAsync(Floor floor, string name, IEnumerable<NameTranslation>? translations, ConstraintOverrides overrides)
    {
        var building = await GetBuildingAsync(floor.BuildingId);
        var (english, extras) = await CheckAsync(building, name, translations, overrides, floor.Id);
        floor.SetName(PortalLanguages.Default, english);
        foreach (var t in extras) floor.SetName(t.Language, t.Name);
        floor.RemoveTranslationsExcept(extras.Select(t => t.Language));
        ApplyOverrides(floor, overrides);
    }

    private async Task<Building> GetBuildingAsync(Guid id)
        => await _buildings.FindAsync(id)
           ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:FloorBuildingMissing"]).ForField("buildingId");

    private async Task<(string English, List<NameTranslation> Extras)> CheckAsync(Building building, string? name,
        IEnumerable<NameTranslation>? translations, ConstraintOverrides o, Guid? excludeId)
    {
        var english = await CheckNameAsync(building, PortalLanguages.Default, name, excludeId, "name");
        var extras = TranslationRules.Clean(L, translations, FloorConsts.MaxNameLength);
        foreach (var t in extras) await CheckNameAsync(building, t.Language, t.Name, excludeId, $"translations.{t.Language}");
        EstateOverrideRules.EnsureOnlyNarrows(L, EstateOverrideRules.FloorLevel,
            ConstraintResolver.ResolveBounds(building, null),
            o.OpenHour, o.CloseHour, o.MinBookingMinutes, o.MaxBookingHours);
        return (english, extras);
    }

    private async Task<string> CheckNameAsync(Building building, string language, string? name, Guid? excludeId, string field)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:FloorNameMissing"]).ForField(field);
        ScriptRules.EnsureFits(L, language, trimmed, field);
        if (await _floors.AnyAsync(f => f.BuildingId == building.Id && f.Id != excludeId
                && f.Translations.Any(t => t.Language == language && t.Name == trimmed)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.FloorDuplicate, message:
                L["Error:FloorDuplicate", building.GetName(), trimmed]).ForField(field);
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
