using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

[Authorize]
public class BuildingAppService : EstateAppServiceBase, IBuildingAppService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingDtoMapper _mapper;

    public BuildingAppService(IRepository<Building, Guid> buildings, BuildingDtoMapper mapper)
    {
        _buildings = buildings;
        _mapper = mapper;
    }

    public async Task<ListResultDto<BuildingDto>> GetListAsync()
    {
        var buildings = await _buildings.GetListAsync();
        return new ListResultDto<BuildingDto>(await _mapper.MapListAsync(buildings.OrderBy(b => b.Name)));
    }

    public async Task<BuildingDto> GetAsync(Guid id) => await _mapper.MapAsync(await _buildings.GetAsync(id));

    [Authorize(PortalPermissions.Buildings.Manage)]
    public async Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input)
    {
        var name = input.Name.Trim();
        await ValidateAsync(input, name, null);
        var building = new Building(GuidGenerator.Create(), name);
        Apply(building, input);
        await _buildings.InsertAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Manage)]
    public async Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input)
    {
        var building = await _buildings.GetAsync(id);
        var name = input.Name.Trim();
        await ValidateAsync(input, name, id);
        building.Name = name;
        Apply(building, input);
        await _buildings.UpdateAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Manage)]
    public async Task<BuildingDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var building = await _buildings.GetAsync(id);
        building.IsBookable = input.IsBookable;
        await _buildings.UpdateAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }

    private async Task ValidateAsync(CreateUpdateBuildingDto input, string name, Guid? excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the building a name.");
        var lower = name.ToLower();
        if (await _buildings.AnyAsync(b => b.Name.ToLower() == lower && b.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.BuildingDuplicate, $"{name} is already in the estate.");
        if (input.CloseHour <= input.OpenHour)
            throw new BusinessException(PortalDomainErrorCodes.InvalidHours, "Close hour must be after open hour.");
    }

    private static void Apply(Building b, CreateUpdateBuildingDto input)
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
