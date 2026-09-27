using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Permissions;
using Dixels.Portal.SpaceTypes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

/* Reading is open to every signed-in user (finding and booking rooms); changes need the Spaces permissions. */
[Authorize]
public class SpaceAppService
    : CrudAppService<Space, SpaceDto, Guid, EstateListInput, CreateUpdateSpaceDto>, ISpaceAppService
{
    /* A registry page never shows more than this many rows, whatever the client asks for. */
    private const int MaxRegistryPageSize = 100;

    private readonly SpaceManager _spaceManager;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Booking, Guid> _bookings;

    public SpaceAppService(IRepository<Space, Guid> repository, SpaceManager spaceManager,
        IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<SpaceType, Guid> types, IRepository<Booking, Guid> bookings)
        : base(repository)
    {
        _spaceManager = spaceManager;
        _buildings = buildings;
        _floors = floors;
        _types = types;
        _bookings = bookings;
        LocalizationResource = typeof(PortalResource);
        CreatePolicyName = PortalPermissions.Spaces.Create;
        UpdatePolicyName = PortalPermissions.Spaces.Edit;
        DeletePolicyName = PortalPermissions.Spaces.Delete;
    }

    public override async Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
    {
        await CheckCreatePolicyAsync();
        var space = await _spaceManager.CreateAsync(input.Name, input.BuildingId, input.FloorId, input.TypeId, Overrides(input));
        CopyFields(space, input);
        await Repository.InsertAsync(space, autoSave: true);
        return await MapToGetOutputDtoAsync(space);
    }

    public override async Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input)
    {
        await CheckUpdatePolicyAsync();
        var space = await GetEntityByIdAsync(id);
        await _spaceManager.UpdateAsync(space, input.Name, input.BuildingId, input.FloorId, input.TypeId, Overrides(input));
        CopyFields(space, input);
        await Repository.UpdateAsync(space, autoSave: true);
        return await MapToGetOutputDtoAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Edit)]
    public async Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var space = await GetEntityByIdAsync(id);
        space.IsBookable = input.IsBookable;
        await Repository.UpdateAsync(space, autoSave: true);
        return await MapToGetOutputDtoAsync(space);
    }

    /* "Find a space": bookable spaces matching the filters, filtered in the database. */
    public async Task<ListResultDto<SpaceDto>> GetBookableListAsync(FindSpacesInput input)
    {
        var name = input.Name?.Trim().ToLower();
        var floorName = input.FloorName?.Trim();
        var minCapacity = input.MinCapacity ?? 0;
        var typeIds = input.TypeIds ?? new();

        /* Bookable = the building, the floor and the space are all bookable (same rule as BookingManager.CanBook;
         * a space whose floor no longer exists is judged by its building and itself). */
        var rows =
            from s in await Repository.GetQueryableAsync()
            join b in await _buildings.GetQueryableAsync() on s.BuildingId equals b.Id
            join f in await _floors.GetQueryableAsync() on s.FloorId equals f.Id into floorJoin
            from f in floorJoin.DefaultIfEmpty()
            where s.IsBookable && b.IsBookable && (f == null || f.IsBookable)
            select new { Space = s, BuildingName = b.Name, FloorName = f == null ? null : f.Name };

        rows = rows
            .WhereIf(input.BuildingId.HasValue, r => r.Space.BuildingId == input.BuildingId)
            .WhereIf(!string.IsNullOrEmpty(floorName), r => r.FloorName == floorName)
            .WhereIf(minCapacity > 0, r => r.Space.Capacity >= minCapacity)
            .WhereIf(typeIds.Count > 0, r => typeIds.Contains(r.Space.TypeId))
            .WhereIf(!string.IsNullOrEmpty(name), r => r.Space.Name.ToLower().Contains(name!));

        var spaces = await AsyncExecuter.ToListAsync(rows.OrderBy(r => r.BuildingName).ThenBy(r => r.Space.Name).Select(r => r.Space));
        return new ListResultDto<SpaceDto>(await MapToGetListOutputDtosAsync(spaces));
    }

    /* One filtered page for the admin registry, with each space's upcoming-booking count. */
    public async Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input)
    {
        var filtered = (await Repository.GetQueryableAsync()).ApplyRegistryFilter(input);
        var total = await AsyncExecuter.CountAsync(filtered);

        /* Building name, then space name (Id breaks ties so pages never overlap). */
        var ordered = from s in filtered
                      join b in await _buildings.GetQueryableAsync() on s.BuildingId equals b.Id
                      orderby b.Name, s.Name, s.Id
                      select s;
        var take = Math.Clamp(input.MaxResultCount, 1, MaxRegistryPageSize);
        var page = await AsyncExecuter.ToListAsync(ordered.Skip(Math.Max(0, input.SkipCount)).Take(take));

        return new SpaceRegistryPageDto
        {
            TotalCount = total,
            Items = await MapToGetListOutputDtosAsync(page),
            UpcomingBookingCounts = await CountUpcomingBookingsAsync(page.Select(s => s.Id).ToList()),
        };
    }

    /* The full list (pickers) is ordered by building name, then space name. */
    protected override async Task<IQueryable<Space>> CreateFilteredQueryAsync(EstateListInput input)
        => from s in await base.CreateFilteredQueryAsync(input)
           join b in await _buildings.GetQueryableAsync() on s.BuildingId equals b.Id
           orderby b.Name, s.Name
           select s;

    /* Keep the building-then-name order set above instead of ABP's default (creation time). */
    protected override IQueryable<Space> ApplyDefaultSorting(IQueryable<Space> query) => query;

    protected override async Task<SpaceDto> MapToGetOutputDtoAsync(Space entity)
        => (await MapToGetListOutputDtosAsync(new List<Space> { entity }))[0];

    /* Building, floor and type for every space on the list (one query each), then the rules the space
     * actually follows and whether it can be booked. */
    protected override async Task<List<SpaceDto>> MapToGetListOutputDtosAsync(List<Space> entities)
    {
        var buildingIds = entities.Select(s => s.BuildingId).Distinct().ToList();
        var floorIds = entities.Select(s => s.FloorId).Distinct().ToList();
        var typeIds = entities.Select(s => s.TypeId).Distinct().ToList();
        var buildings = (await _buildings.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);
        var typeNames = (await _types.GetListAsync(t => typeIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Name);

        return entities.Select(s =>
        {
            var building = buildings[s.BuildingId];
            var floor = floors.GetValueOrDefault(s.FloorId);
            var ctx = new SpaceContext(s, floor, building);
            var c = ctx.Constraints;

            var dto = ObjectMapper.Map<Space, SpaceDto>(s);
            dto.TypeName = typeNames.GetValueOrDefault(s.TypeId, "Unknown type");
            dto.BuildingName = building.Name;
            dto.FloorName = floor?.Name ?? "";
            dto.TimeZone = building.TimeZone;
            dto.Constraints = new ResolvedConstraintsDto
            {
                OpenMinute = c.OpenMinute,
                CloseMinute = c.CloseMinute,
                MinBookingMinutes = c.MinBookingMinutes,
                MaxBookingHours = c.MaxBookingHours,
                Holidays = c.Holidays.ToList(),
            };
            dto.CanCurrentUserBook = BookingManager.CanBook(ctx);
            dto.NotBookableReason = BookingManager.FindNotBookableReason(ctx);
            return dto;
        }).ToList();
    }

    /* Only for the spaces on the current page, in one grouped query. */
    private async Task<Dictionary<Guid, int>> CountUpcomingBookingsAsync(List<Guid> spaceIds)
    {
        if (spaceIds.Count == 0) return new();
        var now = Clock.Now;
        var counts = await AsyncExecuter.ToListAsync((await _bookings.GetQueryableAsync())
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.EndUtc > now)
            .GroupBy(b => b.SpaceId)
            .Select(g => new { SpaceId = g.Key, Count = g.Count() }));
        return counts.ToDictionary(c => c.SpaceId, c => c.Count);
    }

    private static void CopyFields(Space s, CreateUpdateSpaceDto input)
    {
        s.IsBookable = input.IsBookable;
        s.Capacity = Math.Max(0, input.Capacity);
        s.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
    }

    private static ConstraintOverrides Overrides(CreateUpdateSpaceDto input) => new(
        input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
}
