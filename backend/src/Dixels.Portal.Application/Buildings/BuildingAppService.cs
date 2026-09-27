using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings;

/* Every action needs its Buildings permission: Default to read, Create / Edit / Delete to change. */
[Authorize]
public class BuildingAppService
    : CrudAppService<Building, BuildingDto, Guid, EstateListInput, CreateUpdateBuildingDto>, IBuildingAppService
{
    private readonly BuildingManager _buildingManager;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public BuildingAppService(IRepository<Building, Guid> repository, BuildingManager buildingManager,
        IRepository<Floor, Guid> floors, IRepository<Space, Guid> spaces)
        : base(repository)
    {
        _buildingManager = buildingManager;
        _floors = floors;
        _spaces = spaces;
        LocalizationResource = typeof(PortalResource);
    }

    [Authorize(PortalPermissions.Buildings.Default)]
    public override Task<PagedResultDto<BuildingDto>> GetListAsync(EstateListInput input) => base.GetListAsync(input);

    [Authorize(PortalPermissions.Buildings.Default)]
    public override Task<BuildingDto> GetAsync(Guid id) => base.GetAsync(id);

    [Authorize(PortalPermissions.Buildings.Delete)]
    public override Task DeleteAsync(Guid id) => base.DeleteAsync(id);

    [Authorize(PortalPermissions.Buildings.Create)]
    public override async Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input)
    {
        var building = await _buildingManager.CreateAsync(input.Name, input.OpenHour, input.CloseHour);
        CopyFields(building, input);
        await Repository.InsertAsync(building, autoSave: true);
        return await MapToGetOutputDtoAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Edit)]
    public override async Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input)
    {
        var building = await GetEntityByIdAsync(id);
        await _buildingManager.ChangeNameAsync(building, input.Name);
        BuildingManager.EnsureValidHours(input.OpenHour, input.CloseHour);
        CopyFields(building, input);
        await Repository.UpdateAsync(building, autoSave: true);
        return await MapToGetOutputDtoAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Edit)]
    public async Task<BuildingDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var building = await GetEntityByIdAsync(id);
        building.IsBookable = input.IsBookable;
        await Repository.UpdateAsync(building, autoSave: true);
        return await MapToGetOutputDtoAsync(building);
    }

    protected override IQueryable<Building> ApplyDefaultSorting(IQueryable<Building> query) => query.OrderBy(b => b.Name);

    protected override async Task<BuildingDto> MapToGetOutputDtoAsync(Building entity)
        => (await MapToGetListOutputDtosAsync(new List<Building> { entity }))[0];

    /* Floor and space counts for every building on the list, in two grouped queries. */
    protected override async Task<List<BuildingDto>> MapToGetListOutputDtosAsync(List<Building> entities)
    {
        var ids = entities.Select(b => b.Id).ToList();
        var floorCounts = (await AsyncExecuter.ToListAsync((await _floors.GetQueryableAsync())
                .Where(f => ids.Contains(f.BuildingId)).GroupBy(f => f.BuildingId).Select(g => new { g.Key, Count = g.Count() })))
            .ToDictionary(x => x.Key, x => x.Count);
        var spaceCounts = (await AsyncExecuter.ToListAsync((await _spaces.GetQueryableAsync())
                .Where(s => ids.Contains(s.BuildingId)).GroupBy(s => s.BuildingId).Select(g => new { g.Key, Count = g.Count() })))
            .ToDictionary(x => x.Key, x => x.Count);

        return entities.Select(b =>
        {
            var dto = ObjectMapper.Map<Building, BuildingDto>(b);
            dto.FloorCount = floorCounts.GetValueOrDefault(b.Id);
            dto.SpaceCount = spaceCounts.GetValueOrDefault(b.Id);
            return dto;
        }).ToList();
    }

    private static void CopyFields(Building b, CreateUpdateBuildingDto input)
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
