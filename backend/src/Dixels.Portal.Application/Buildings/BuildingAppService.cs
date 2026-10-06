using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
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
    public override async Task DeleteAsync(Guid id)
    {
        var building = await GetEntityByIdAsync(id);
        await _buildingManager.EnsureCanDeleteAsync(building);
        await Repository.DeleteAsync(building, autoSave: true);
    }

    [Authorize(PortalPermissions.Buildings.Create)]
    public override async Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input)
    {
        var building = await _buildingManager.CreateAsync(input.Name, input.Translations.ToNameTranslations(), input.OpenHour, input.CloseHour);
        CopyFields(building, input);
        await Repository.InsertAsync(building, autoSave: true);
        return await MapToGetOutputDtoAsync(building);
    }

    [Authorize(PortalPermissions.Buildings.Edit)]
    public override async Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input)
    {
        var building = await GetEntityByIdAsync(id);
        await _buildingManager.ChangeNamesAsync(building, input.Name, input.Translations.ToNameTranslations());
        _buildingManager.EnsureValidHours(input.OpenHour, input.CloseHour);
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

    protected override IQueryable<Building> ApplyDefaultSorting(IQueryable<Building> query)
        => query.OrderBy(LocalizedNameQuery.BuildingName(PortalLanguages.Current));

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
            dto.Name = b.GetName();
            dto.Translations = TranslationDtos.Of(b.Translations, t => new TranslationDto { Language = t.Language, Name = t.Name });
            dto.FloorCount = floorCounts.GetValueOrDefault(b.Id);
            dto.SpaceCount = spaceCounts.GetValueOrDefault(b.Id);
            return dto;
        }).ToList();
    }

    private void CopyFields(Building b, CreateUpdateBuildingDto input)
    {
        var tz = string.IsNullOrWhiteSpace(input.TimeZone) ? "UTC" : input.TimeZone.Trim();
        if (!BuildingCalendar.IsKnownZone(tz))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UnknownTimeZone, message: L["Error:UnknownTimeZone", tz]).ForField("timeZone");
        if (input.ClosedWeekdays.Any(d => d is < 0 or > 6))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidWeekday, message: L["Error:InvalidWeekday"]).ForField("closedWeekdays");
        b.TimeZone = tz;
        b.IsBookable = input.IsBookable;
        b.OpenHour = input.OpenHour;
        b.CloseHour = input.CloseHour;
        b.MinBookingMinutes = input.MinBookingMinutes;
        b.MaxBookingHours = input.MaxBookingHours;
        b.Holidays = input.Holidays.Distinct().OrderBy(d => d).ToList();
        b.ClosedWeekdays = input.ClosedWeekdays.Distinct().OrderBy(d => d).ToList();
    }
}
