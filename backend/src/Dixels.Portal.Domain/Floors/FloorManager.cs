using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

/* Floor rules: a name unique within its building (per language), and overrides that only narrow the building's rules. */
public class FloorManager : PortalDomainService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public FloorManager(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors)
    {
        _buildings = buildings;
        _floors = floors;
    }

    /* The name is saved in the language the admin is using. */
    public async Task<Floor> CreateAsync(Guid buildingId, string name, ConstraintOverrides overrides)
    {
        var language = PortalLanguages.Current;
        var building = await GetBuildingAsync(buildingId);
        var trimmed = await CheckAsync(building, name, language, overrides, null);
        var floor = new Floor(GuidGenerator.Create(), building.Id, language, trimmed);
        ApplyOverrides(floor, overrides);
        return floor;
    }

    /* A floor stays in its building: editing can change its name and rules, not move it.
     * Renames it in the admin's language only; saving the fallback name unchanged adds no translation. */
    public async Task UpdateAsync(Floor floor, string name, ConstraintOverrides overrides)
    {
        var language = PortalLanguages.Current;
        var building = await GetBuildingAsync(floor.BuildingId);
        var trimmed = name?.Trim() ?? "";
        var keepsFallback = floor.FindTranslation(language) == null && trimmed == floor.GetName(language);
        trimmed = await CheckAsync(building, trimmed, language, overrides, floor.Id, checkName: !keepsFallback);
        if (!keepsFallback) floor.SetName(language, trimmed);
        ApplyOverrides(floor, overrides);
    }

    private async Task<Building> GetBuildingAsync(Guid id)
        => await _buildings.FindAsync(id)
           ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:FloorBuildingMissing"]).ForField("buildingId");

    private async Task<string> CheckAsync(Building building, string? name, string language, ConstraintOverrides o, Guid? excludeId,
        bool checkName = true)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:FloorNameMissing"]).ForField("name");
        if (checkName && await _floors.AnyAsync(f => f.BuildingId == building.Id && f.Id != excludeId
                && f.Translations.Any(t => t.Language == language && t.Name == trimmed)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.FloorDuplicate, message:
                L["Error:FloorDuplicate", building.GetName(), trimmed]).ForField("name");
        EstateOverrideRules.EnsureOnlyNarrows(L, EstateOverrideRules.FloorLevel,
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
