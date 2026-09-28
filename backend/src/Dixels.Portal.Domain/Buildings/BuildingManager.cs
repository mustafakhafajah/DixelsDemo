using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Buildings;

/* Building rules that need the database: a unique name. Creates the entity; the app service saves it. */
public class BuildingManager : DomainService
{
    private readonly IRepository<Building, Guid> _buildings;

    public BuildingManager(IRepository<Building, Guid> buildings)
    {
        _buildings = buildings;
    }

    public async Task<Building> CreateAsync(string name, int openHour, int closeHour)
    {
        var trimmed = await CheckNameAsync(name, null);
        EnsureValidHours(openHour, closeHour);
        return new Building(GuidGenerator.Create(), trimmed);
    }

    public async Task ChangeNameAsync(Building building, string name)
    {
        building.Name = await CheckNameAsync(name, building.Id);
    }

    public static void EnsureValidHours(int openHour, int closeHour)
    {
        if (closeHour <= openHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidHours, message: "Close hour must be after open hour.");
    }

    /* Returns the trimmed name. excludeId is the building being edited, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string? name, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: "Give the building a name.");
        var lower = trimmed.ToLower();
        if (await _buildings.AnyAsync(b => b.Name.ToLower() == lower && b.Id != excludeId))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BuildingDuplicate, message: $"{trimmed} is already in the estate.");
        return trimmed;
    }
}
