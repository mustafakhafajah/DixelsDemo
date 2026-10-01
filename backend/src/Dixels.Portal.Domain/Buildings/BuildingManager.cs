using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

/* Building rules that need the database: a name unique within each language. Creates the entity; the app service saves it. */
public class BuildingManager : PortalDomainService
{
    private readonly IRepository<Building, Guid> _buildings;

    public BuildingManager(IRepository<Building, Guid> buildings)
    {
        _buildings = buildings;
    }

    /* The name is saved in the language the admin is using. */
    public async Task<Building> CreateAsync(string name, int openHour, int closeHour)
    {
        var language = PortalLanguages.Current;
        var trimmed = await CheckNameAsync(name, language, null);
        EnsureValidHours(openHour, closeHour);
        return new Building(GuidGenerator.Create(), language, trimmed);
    }

    /* Renames it in the admin's language only. Saving the fallback name unchanged adds no translation. */
    public async Task ChangeNameAsync(Building building, string name)
    {
        var language = PortalLanguages.Current;
        var trimmed = name?.Trim() ?? "";
        if (building.FindTranslation(language) == null && trimmed == building.GetName(language)) return;
        building.SetName(language, await CheckNameAsync(trimmed, language, building.Id));
    }

    public void EnsureValidHours(int openHour, int closeHour)
    {
        if (closeHour <= openHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidHours, message: L["Error:CloseBeforeOpen"]).ForField("closeHour");
    }

    /* Returns the trimmed name. excludeId is the building being edited, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string? name, string language, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:BuildingNameMissing"]).ForField("name");
        var lower = trimmed.ToLower();
        if (await _buildings.AnyAsync(b => b.Id != excludeId && b.Translations.Any(t => t.Language == language && t.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BuildingDuplicate, message: L["Error:BuildingDuplicate", trimmed]).ForField("name");
        return trimmed;
    }
}
