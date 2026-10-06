using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

/* Building rules that need the database: an English name, optional names in other languages, each unique
 * within its language; and only an empty building can be deleted. Creates the entity; the app service saves it. */
public class BuildingManager : PortalDomainService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public BuildingManager(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors, IRepository<Space, Guid> spaces)
    {
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
    }

    public async Task<Building> CreateAsync(string name, IEnumerable<NameTranslation>? translations, int openHour, int closeHour)
    {
        var english = await CheckNameAsync(PortalLanguages.Default, name, null, "name");
        var extras = await CheckTranslationsAsync(translations, null);
        EnsureValidHours(openHour, closeHour);
        var building = new Building(GuidGenerator.Create(), english);
        foreach (var t in extras) building.SetName(t.Language, t.Name);
        return building;
    }

    /* translations is the full set of extra languages: one left out is removed. */
    public async Task ChangeNamesAsync(Building building, string name, IEnumerable<NameTranslation>? translations)
    {
        var english = await CheckNameAsync(PortalLanguages.Default, name, building.Id, "name");
        var extras = await CheckTranslationsAsync(translations, building.Id);
        building.SetName(PortalLanguages.Default, english);
        foreach (var t in extras) building.SetName(t.Language, t.Name);
        building.RemoveTranslationsExcept(extras.Select(t => t.Language));
    }

    public void EnsureValidHours(int openHour, int closeHour)
    {
        if (closeHour <= openHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidHours, message: L["Error:CloseBeforeOpen"]).ForField("closeHour");
    }

    /* Deleting a building would leave its floors, spaces and their bookings pointing at nothing, so they go first
     * (deleting a space checks its bookings). */
    public async Task EnsureCanDeleteAsync(Building building)
    {
        if (await _floors.AnyAsync(f => f.BuildingId == building.Id) || await _spaces.AnyAsync(s => s.BuildingId == building.Id))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BuildingNotEmpty, message: L["Error:BuildingNotEmpty", building.GetName()]);
    }

    private async Task<List<NameTranslation>> CheckTranslationsAsync(IEnumerable<NameTranslation>? input, Guid? excludeId)
    {
        var extras = TranslationRules.Clean(L, input, BuildingConsts.MaxNameLength);
        foreach (var t in extras) await CheckNameAsync(t.Language, t.Name, excludeId, $"translations.{t.Language}");
        return extras;
    }

    /* Returns the trimmed name. excludeId is the building being edited, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string language, string? name, Guid? excludeId, string field)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:BuildingNameMissing"]).ForField(field);
        ScriptRules.EnsureFits(L, language, trimmed, field);
        var lower = trimmed.ToLower();
        if (await _buildings.AnyAsync(b => b.Id != excludeId && b.Translations.Any(t => t.Language == language && t.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BuildingDuplicate, message: L["Error:BuildingDuplicate", trimmed]).ForField(field);
        return trimmed;
    }
}
