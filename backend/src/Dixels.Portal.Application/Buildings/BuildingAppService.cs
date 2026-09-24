using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
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
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public BuildingAppService(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<Space, Guid> spaces)
    {
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
    }

    public async Task<ListResultDto<BuildingDto>> GetListAsync()
    {
        var buildings = await _buildings.GetListAsync();
        var floors = await _floors.GetListAsync();
        var spaces = await _spaces.GetListAsync();
        return new ListResultDto<BuildingDto>(buildings.OrderBy(b => b.Name)
            .Select(b => Map(b, floors.Count(f => f.BuildingId == b.Id), spaces.Count(s => s.BuildingId == b.Id)))
            .ToList());
    }

    public async Task<BuildingDto> GetAsync(Guid id) => await MapWithCountsAsync(await _buildings.GetAsync(id));

    [Authorize(PortalPermissions.Buildings.Manage)]
    public async Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input)
    {
        var name = input.Name.Trim();
        await ValidateAsync(input, name, null);
        var building = new Building(GuidGenerator.Create(), name);
        Apply(building, input);
        await _buildings.InsertAsync(building, autoSave: true);
        return await MapWithCountsAsync(building);
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
        return await MapWithCountsAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Manage)]
    public async Task<BuildingDto> SetStatusAsync(Guid id, SetStatusDto input)
    {
        var building = await _buildings.GetAsync(id);
        building.Status = input.Status;
        await _buildings.UpdateAsync(building, autoSave: true);
        return await MapWithCountsAsync(building);
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
        b.Status = input.Status;
        b.OpenHour = input.OpenHour;
        b.CloseHour = input.CloseHour;
        b.MinBookingMinutes = input.MinBookingMinutes;
        b.MaxBookingHours = input.MaxBookingHours;
        b.Holidays = input.Holidays.Distinct().OrderBy(d => d).ToList();
    }

    private async Task<BuildingDto> MapWithCountsAsync(Building b)
        => Map(b, await _floors.CountAsync(f => f.BuildingId == b.Id), await _spaces.CountAsync(s => s.BuildingId == b.Id));

    private static BuildingDto Map(Building b, int floorCount, int spaceCount) => new()
    {
        Id = b.Id,
        Name = b.Name,
        TimeZone = b.TimeZone,
        Status = b.Status,
        OpenHour = b.OpenHour,
        CloseHour = b.CloseHour,
        MinBookingMinutes = b.MinBookingMinutes,
        MaxBookingHours = b.MaxBookingHours,
        Holidays = b.Holidays.ToList(),
        FloorCount = floorCount,
        SpaceCount = spaceCount,
    };
}
