using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Estate;

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

[Authorize]
public class FloorAppService : EstateAppServiceBase, IFloorAppService
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public FloorAppService(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<Space, Guid> spaces)
    {
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
    }

    public async Task<ListResultDto<FloorDto>> GetListAsync(Guid? buildingId)
    {
        var buildings = (await _buildings.GetListAsync()).ToDictionary(b => b.Id);
        var floors = buildingId.HasValue
            ? await _floors.GetListAsync(f => f.BuildingId == buildingId.Value)
            : await _floors.GetListAsync();
        var spaces = await _spaces.GetListAsync();
        return new ListResultDto<FloorDto>(floors
            .OrderBy(f => buildings[f.BuildingId].Name)
            .ThenBy(f => f.Name.PadLeft(8, '0'))
            .Select(f => Map(f, buildings[f.BuildingId], spaces.Count(s => s.FloorId == f.Id)))
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
        return Map(floor, building, 0);
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
        return Map(floor, building, await _spaces.CountAsync(s => s.FloorId == floor.Id));
    }

    [Authorize(PortalPermissions.Floors.Manage)]
    public async Task<FloorDto> SetStatusAsync(Guid id, SetStatusDto input)
    {
        var floor = await _floors.GetAsync(id);
        var building = await GetBuildingAsync(floor.BuildingId);
        floor.Status = input.Status;
        await _floors.UpdateAsync(floor, autoSave: true);
        return Map(floor, building, await _spaces.CountAsync(s => s.FloorId == floor.Id));
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
        EstateValidation.ValidateOverrides("Floor", "the building", "the building's",
            ConstraintResolver.ResolveBounds(b, null),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
    }

    private static void Apply(Floor f, CreateUpdateFloorDto input)
    {
        f.Status = input.Status;
        f.OpenHourOverride = input.OpenHourOverride;
        f.CloseHourOverride = input.CloseHourOverride;
        f.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        f.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }

    private static FloorDto Map(Floor f, Building b, int spaceCount) => new()
    {
        Id = f.Id,
        BuildingId = f.BuildingId,
        BuildingName = b.Name,
        Name = f.Name,
        Status = f.Status,
        OpenHourOverride = f.OpenHourOverride,
        CloseHourOverride = f.CloseHourOverride,
        MinBookingMinutesOverride = f.MinBookingMinutesOverride,
        MaxBookingHoursOverride = f.MaxBookingHoursOverride,
        SpaceCount = spaceCount,
    };
}

public static class EstateValidation
{
    /* Overrides may only narrow the parent's rules (mock: submitFloor / submitSpace). */
    public static void ValidateOverrides(string subject, string parent, string parentPossessive, ResolvedBounds bounds,
        int? openOv, int? closeOv, int? minOv, int? maxOv)
    {
        if (openOv.HasValue && openOv < bounds.OpenHour)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject} cannot open earlier ({openOv}) than {parent} ({bounds.OpenHour}).");
        if (closeOv.HasValue && closeOv > bounds.CloseHour)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject} cannot close later ({closeOv}) than {parent} ({bounds.CloseHour}).");
        var effOpen = openOv ?? bounds.OpenHour;
        var effClose = closeOv ?? bounds.CloseHour;
        if (effClose <= effOpen)
            throw new BusinessException(PortalDomainErrorCodes.InvalidHours, "Close hour must be after open hour.");
        if (minOv.HasValue && minOv < bounds.MinBookingMinutes)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject}'s minimum duration cannot be less than {parentPossessive} ({bounds.MinBookingMinutes}m).");
        if (maxOv.HasValue && maxOv > bounds.MaxBookingHours)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject}'s maximum duration cannot exceed {parentPossessive} ({bounds.MaxBookingHours}h).");
    }
}
