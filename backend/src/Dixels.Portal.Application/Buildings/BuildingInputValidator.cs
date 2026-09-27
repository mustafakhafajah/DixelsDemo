using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

/* The rules a building form must pass, shared by the create and update commands. */
public class BuildingInputValidator : ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;

    public BuildingInputValidator(IRepository<Building, Guid> buildings)
    {
        _buildings = buildings;
    }

    /* Returns the trimmed name. excludeId is the building being edited, so it doesn't clash with itself. */
    public async Task<string> ValidateAsync(CreateUpdateBuildingDto input, Guid? excludeId)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the building a name.");
        var lower = name.ToLower();
        if (await _buildings.AnyAsync(b => b.Name.ToLower() == lower && b.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.BuildingDuplicate, $"{name} is already in the estate.");
        if (input.CloseHour <= input.OpenHour)
            throw new BusinessException(PortalDomainErrorCodes.InvalidHours, "Close hour must be after open hour.");
        return name;
    }

    public static void Apply(Building b, CreateUpdateBuildingDto input)
    {
        b.TimeZone = string.IsNullOrWhiteSpace(input.TimeZone) ? "UTC" : input.TimeZone.Trim();
        b.IsBookable = input.IsBookable;
        b.OpenHour = input.OpenHour;
        b.CloseHour = input.CloseHour;
        b.MinBookingMinutes = input.MinBookingMinutes;
        b.MaxBookingHours = input.MaxBookingHours;
        b.Holidays = input.Holidays.Distinct().OrderBy(d => d).ToList();
    }
}
