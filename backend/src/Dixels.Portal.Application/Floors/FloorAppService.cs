using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

[Authorize]
public class FloorAppService : EstateAppServiceBase, IFloorAppService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly FloorDtoMapper _mapper;

    public FloorAppService(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors, FloorDtoMapper mapper)
    {
        _buildings = buildings;
        _floors = floors;
        _mapper = mapper;
    }

    public async Task<ListResultDto<FloorDto>> GetListAsync(Guid? buildingId)
    {
        var floors = buildingId.HasValue
            ? await _floors.GetListAsync(f => f.BuildingId == buildingId.Value)
            : await _floors.GetListAsync();
        var dtos = await _mapper.MapListAsync(floors);
        return new ListResultDto<FloorDto>(dtos
            .OrderBy(f => f.BuildingName)
            .ThenBy(f => f.Name.PadLeft(8, '0'))
            .ToList());
    }

    [Authorize(PortalPermissions.Floors.Manage)]
    public async Task<FloorDto> CreateAsync(CreateUpdateFloorDto input)
    {
        var building = await GetBuildingAsync(input.BuildingId);
        var name = input.Name.Trim();
        await ValidateAsync(building, input, name, null);
        var floor = new Floor(GuidGenerator.Create(), building.Id, name);
        Apply(floor, input);
        await _floors.InsertAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }

    [Authorize(PortalPermissions.Floors.Manage)]
    public async Task<FloorDto> UpdateAsync(Guid id, CreateUpdateFloorDto input)
    {
        var floor = await _floors.GetAsync(id);
        var building = await GetBuildingAsync(floor.BuildingId);
        var name = input.Name.Trim();
        await ValidateAsync(building, input, name, id);
        floor.Name = name;
        Apply(floor, input);
        await _floors.UpdateAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }

    [Authorize(PortalPermissions.Floors.Manage)]
    public async Task<FloorDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var floor = await _floors.GetAsync(id);
        floor.IsBookable = input.IsBookable;
        await _floors.UpdateAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }

    private async Task<Building> GetBuildingAsync(Guid id)
        => await _buildings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building first.");

    private async Task ValidateAsync(Building b, CreateUpdateFloorDto input, string name, Guid? excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Type a floor name or number.");
        if (await _floors.AnyAsync(f => f.BuildingId == b.Id && f.Name == name && f.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.FloorDuplicate, $"{b.Name} already has floor {name}.");
        EstateOverrideRules.EnsureOnlyNarrows("Floor", "the building", "the building's",
            ConstraintResolver.ResolveBounds(b, null),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
    }

    private static void Apply(Floor f, CreateUpdateFloorDto input)
    {
        f.IsBookable = input.IsBookable;
        f.OpenHourOverride = input.OpenHourOverride;
        f.CloseHourOverride = input.CloseHourOverride;
        f.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        f.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }
}
