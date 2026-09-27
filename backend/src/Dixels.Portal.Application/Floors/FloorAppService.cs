using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors;

/* Reading is open to every signed-in user (booking screens list floors); changes need the Floors permissions. */
[Authorize]
public class FloorAppService
    : CrudAppService<Floor, FloorDto, Guid, GetFloorListInput, CreateUpdateFloorDto>, IFloorAppService
{
    private readonly FloorManager _floorManager;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Space, Guid> _spaces;

    public FloorAppService(IRepository<Floor, Guid> repository, FloorManager floorManager,
        IRepository<Building, Guid> buildings, IRepository<Space, Guid> spaces)
        : base(repository)
    {
        _floorManager = floorManager;
        _buildings = buildings;
        _spaces = spaces;
        LocalizationResource = typeof(PortalResource);
        CreatePolicyName = PortalPermissions.Floors.Create;
        UpdatePolicyName = PortalPermissions.Floors.Edit;
        DeletePolicyName = PortalPermissions.Floors.Delete;
    }

    /* The whole list, ordered by building name and then floor number ("2" before "10"), which the database
     * can't sort by, so ordering happens after mapping. */
    public override async Task<PagedResultDto<FloorDto>> GetListAsync(GetFloorListInput input)
    {
        await CheckGetListPolicyAsync();
        var floors = await AsyncExecuter.ToListAsync(await CreateFilteredQueryAsync(input));
        var dtos = (await MapToGetListOutputDtosAsync(floors))
            .OrderBy(f => f.BuildingName)
            .ThenBy(f => f.Name.PadLeft(8, '0'))
            .ToList();
        return new PagedResultDto<FloorDto>(dtos.Count, dtos);
    }

    public override async Task<FloorDto> CreateAsync(CreateUpdateFloorDto input)
    {
        await CheckCreatePolicyAsync();
        var floor = await _floorManager.CreateAsync(input.BuildingId, input.Name, Overrides(input));
        floor.IsBookable = input.IsBookable;
        await Repository.InsertAsync(floor, autoSave: true);
        return await MapToGetOutputDtoAsync(floor);
    }

    public override async Task<FloorDto> UpdateAsync(Guid id, CreateUpdateFloorDto input)
    {
        await CheckUpdatePolicyAsync();
        var floor = await GetEntityByIdAsync(id);
        await _floorManager.UpdateAsync(floor, input.Name, Overrides(input));
        floor.IsBookable = input.IsBookable;
        await Repository.UpdateAsync(floor, autoSave: true);
        return await MapToGetOutputDtoAsync(floor);
    }

    [Authorize(PortalPermissions.Floors.Edit)]
    public async Task<FloorDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var floor = await GetEntityByIdAsync(id);
        floor.IsBookable = input.IsBookable;
        await Repository.UpdateAsync(floor, autoSave: true);
        return await MapToGetOutputDtoAsync(floor);
    }

    protected override async Task<IQueryable<Floor>> CreateFilteredQueryAsync(GetFloorListInput input)
        => (await base.CreateFilteredQueryAsync(input))
            .WhereIf(input.BuildingId.HasValue, f => f.BuildingId == input.BuildingId);

    protected override async Task<FloorDto> MapToGetOutputDtoAsync(Floor entity)
        => (await MapToGetListOutputDtosAsync(new List<Floor> { entity }))[0];

    /* Building names and space counts for every floor on the list, in two queries. */
    protected override async Task<List<FloorDto>> MapToGetListOutputDtosAsync(List<Floor> entities)
    {
        var buildingIds = entities.Select(f => f.BuildingId).Distinct().ToList();
        var buildingNames = (await _buildings.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id, b => b.Name);
        var floorIds = entities.Select(f => f.Id).ToList();
        var spaceCounts = (await AsyncExecuter.ToListAsync((await _spaces.GetQueryableAsync())
                .Where(s => floorIds.Contains(s.FloorId)).GroupBy(s => s.FloorId).Select(g => new { g.Key, Count = g.Count() })))
            .ToDictionary(x => x.Key, x => x.Count);

        return entities.Select(f =>
        {
            var dto = ObjectMapper.Map<Floor, FloorDto>(f);
            dto.BuildingName = buildingNames.GetValueOrDefault(f.BuildingId, "");
            dto.SpaceCount = spaceCounts.GetValueOrDefault(f.Id);
            return dto;
        }).ToList();
    }

    private static ConstraintOverrides Overrides(CreateUpdateFloorDto input) => new(
        input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
}
