using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Common;
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

/* Every action needs its Spaces permission: Default to read, Create / Edit / Delete to change. */
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
    }

    [Authorize(PortalPermissions.Spaces.Default)]
    public override Task<PagedResultDto<SpaceDto>> GetListAsync(EstateListInput input) => base.GetListAsync(input);

    [Authorize(PortalPermissions.Spaces.Default)]
    public override Task<SpaceDto> GetAsync(Guid id) => base.GetAsync(id);

    [Authorize(PortalPermissions.Spaces.Delete)]
    public override Task DeleteAsync(Guid id) => base.DeleteAsync(id);

    [Authorize(PortalPermissions.Spaces.Create)]
    public override async Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
    {
        var space = await _spaceManager.CreateAsync(input.Name, input.Note, input.Translations.ToNameTranslations(), input.BuildingId, input.FloorId, input.TypeId, Overrides(input));
        CopyFields(space, input);
        await Repository.InsertAsync(space, autoSave: true);
        return await MapToGetOutputDtoAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Edit)]
    public override async Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input)
    {
        var space = await GetEntityByIdAsync(id);
        await _spaceManager.UpdateAsync(space, input.Name, input.Note, input.Translations.ToNameTranslations(), input.BuildingId, input.FloorId, input.TypeId, Overrides(input));
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
    [Authorize(PortalPermissions.Spaces.Default)]
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
            select new { Space = s, Floor = f };

        /* A name typed in any language matches, so people find a space by whichever name they know. */
        rows = rows
            .WhereIf(input.BuildingId.HasValue, r => r.Space.BuildingId == input.BuildingId)
            .WhereIf(!string.IsNullOrEmpty(floorName), r => r.Floor != null && r.Floor.Translations.Any(t => t.Name == floorName))
            .WhereIf(minCapacity > 0, r => r.Space.Capacity >= minCapacity)
            .WhereIf(typeIds.Count > 0, r => typeIds.Contains(r.Space.TypeId))
            .WhereIf(!string.IsNullOrEmpty(name), r => r.Space.Translations.Any(t => t.Name.ToLower().Contains(name!)));

        /* The whole list comes back, so it is sorted by the names in the reader's language after mapping. */
        var spaces = await AsyncExecuter.ToListAsync(rows.Select(r => r.Space));
        var dtos = (await MapToGetListOutputDtosAsync(spaces))
            .OrderBy(d => d.BuildingName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ListResultDto<SpaceDto>(dtos);
    }

    /* One filtered page for the admin registry, with each space's upcoming-booking count. */
    [Authorize(PortalPermissions.Spaces.Default)]
    public async Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input)
    {
        var filtered = (await Repository.GetQueryableAsync()).ApplyRegistryFilter(input);
        var total = await AsyncExecuter.CountAsync(filtered);

        /* Building name, then space name, in the reader's language. */
        var ordered = OrderByNames(filtered, await _buildings.GetQueryableAsync(), PortalLanguages.Current);
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
        => OrderByNames(await base.CreateFilteredQueryAsync(input), await _buildings.GetQueryableAsync(), PortalLanguages.Current);

    /* Sorted in the database by each name in the given language (see LocalizedNameQuery for the fallback);
     * Id breaks ties so pages never overlap. */
    private static IQueryable<Space> OrderByNames(IQueryable<Space> spaces, IQueryable<Building> buildings, string language)
        => from s in spaces
           join b in buildings on s.BuildingId equals b.Id
           let buildingName = b.Translations
               .Where(t => t.Language == language || t.Language == PortalLanguages.Default).OrderBy(t => t.Language == language ? 0 : 1)
               .Select(t => t.Name).FirstOrDefault()
           let spaceName = s.Translations
               .Where(t => t.Language == language || t.Language == PortalLanguages.Default).OrderBy(t => t.Language == language ? 0 : 1)
               .Select(t => t.Name).FirstOrDefault()
           orderby buildingName, spaceName, s.Id
           select s;

    /* Keep the building-then-name order set above instead of ABP's default (creation time). */
    protected override IQueryable<Space> ApplyDefaultSorting(IQueryable<Space> query) => query;

    protected override async Task<SpaceDto> MapToGetOutputDtoAsync(Space entity)
        => (await MapToGetListOutputDtosAsync(new List<Space> { entity }))[0];

    /* Building, floor and type for every space on the list (one query each), then the rules the space
     * actually follows and whether it can be booked. Names are in the reader's language. */
    protected override async Task<List<SpaceDto>> MapToGetListOutputDtosAsync(List<Space> entities)
    {
        var buildingIds = entities.Select(s => s.BuildingId).Distinct().ToList();
        var floorIds = entities.Select(s => s.FloorId).Distinct().ToList();
        var typeIds = entities.Select(s => s.TypeId).Distinct().ToList();
        var buildings = (await _buildings.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);
        var typeNames = (await _types.GetListAsync(t => typeIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.GetName());

        return entities.Select(s =>
        {
            var building = buildings[s.BuildingId];
            var floor = floors.GetValueOrDefault(s.FloorId);
            var ctx = new SpaceContext(s, floor, building);
            var c = ctx.Constraints;

            var dto = ObjectMapper.Map<Space, SpaceDto>(s);
            dto.Name = s.GetName();
            dto.Note = s.GetNote();
            dto.Translations = TranslationDtos.Of(s.Translations, t => new TranslationDto { Language = t.Language, Name = t.Name, Note = t.Note });
            dto.TypeName = typeNames.GetValueOrDefault(s.TypeId) ?? L["UnknownType"];
            dto.BuildingName = building.GetName();
            dto.FloorName = floor?.GetName() ?? "";
            dto.TimeZone = building.TimeZone;
            dto.Constraints = new ResolvedConstraintsDto
            {
                OpenMinute = c.OpenMinute,
                CloseMinute = c.CloseMinute,
                MinBookingMinutes = c.MinBookingMinutes,
                MaxBookingHours = c.MaxBookingHours,
                Holidays = c.Holidays.ToList(),
                ClosedWeekdays = c.ClosedWeekdays.ToList(),
                TimeZone = c.TimeZone,
            };
            dto.CanCurrentUserBook = BookingManager.CanBook(ctx);
            dto.NotBookableReason = BookingManager.FindNotBookableReason(ctx, L);
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
    }

    private static ConstraintOverrides Overrides(CreateUpdateSpaceDto input) => new(
        input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
}
