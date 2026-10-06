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
using Dixels.Portal.Maintenance;
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
    : CrudAppService<Space, SpaceDto, Guid, GetSpaceListInput, CreateUpdateSpaceDto>, ISpaceAppService
{
    private readonly SpaceManager _spaceManager;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly BookingManager _bookingManager;
    private readonly IBookingLocks _locks;

    public SpaceAppService(IRepository<Space, Guid> repository, SpaceManager spaceManager,
        IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<SpaceType, Guid> types, IRepository<Booking, Guid> bookings,
        IRepository<MaintenanceWindow, Guid> maintenance, BookingManager bookingManager, IBookingLocks locks)
        : base(repository)
    {
        _spaceManager = spaceManager;
        _buildings = buildings;
        _floors = floors;
        _types = types;
        _bookings = bookings;
        _maintenance = maintenance;
        _bookingManager = bookingManager;
        _locks = locks;
        LocalizationResource = typeof(PortalResource);
    }

    /* The one space collection: filtered in the database and ordered by building name, then space name, in the
     * reader's language. "Free only" needs each space's own rules, so it runs after the query and paging follows it. */
    [Authorize(PortalPermissions.Spaces.Default)]
    public override async Task<PagedResultDto<SpaceDto>> GetListAsync(GetSpaceListInput input)
    {
        var buildings = await _buildings.GetQueryableAsync();
        var query = (await Repository.GetQueryableAsync())
            .ApplyListFilter(input)
            .ApplyBookableFilter(input.Bookable, buildings, await _floors.GetQueryableAsync());
        var ordered = OrderByNames(query, buildings, PortalLanguages.Current);
        var skip = Math.Max(0, input.SkipCount);

        if (input.FreeFromUtc.HasValue && input.FreeToUtc.HasValue)
        {
            var free = await KeepFreeAsync(await ContextsOfAsync(await AsyncExecuter.ToListAsync(ordered)),
                input.FreeFromUtc.Value.AsUtc(), input.FreeToUtc.Value.AsUtc());
            return new PagedResultDto<SpaceDto>(free.Count,
                await MapToGetListOutputDtosAsync(free.Skip(skip).Take(input.MaxResultCount).ToList()));
        }

        var total = await AsyncExecuter.CountAsync(query);
        var page = await AsyncExecuter.ToListAsync(ordered.Skip(skip).Take(input.MaxResultCount));
        return new PagedResultDto<SpaceDto>(total, await MapToGetListOutputDtosAsync(page));
    }

    [Authorize(PortalPermissions.Spaces.Default)]
    public override Task<SpaceDto> GetAsync(Guid id) => base.GetAsync(id);

    /* Locked like a booking on the space, so nobody books it between the check and the delete. */
    [Authorize(PortalPermissions.Spaces.Delete)]
    public override async Task DeleteAsync(Guid id)
    {
        await _locks.LockSpacesAsync([id]);
        var space = await GetEntityByIdAsync(id);
        await _spaceManager.EnsureCanDeleteAsync(space);
        await Repository.DeleteAsync(space, autoSave: true);
    }

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

    /* "Free only": the spaces that could be booked for [from, to). No confirmed booking and no active blocked time
     * overlaps it (two queries for all the spaces), and the window fits each space's own rules - the same check a
     * booking gets (closed days, opening hours, min / max length). Past windows count, as on the page. */
    private async Task<List<Space>> KeepFreeAsync(List<SpaceContext> candidates, DateTime fromUtc, DateTime toUtc)
    {
        var ids = candidates.Select(c => c.Space.Id).ToList();
        var booked = await AsyncExecuter.ToListAsync((await _bookings.GetQueryableAsync())
            .Where(new OverlappingBookingsSpecification(fromUtc, toUtc).ToExpression())
            .Where(b => ids.Contains(b.SpaceId)).Select(b => b.SpaceId).Distinct());
        var blocked = await AsyncExecuter.ToListAsync((await _maintenance.GetQueryableAsync())
            .Where(new OverlappingMaintenanceSpecification(fromUtc, toUtc).ToExpression())
            .Where(m => ids.Contains(m.SpaceId)).Select(m => m.SpaceId).Distinct());
        var taken = booked.Concat(blocked).ToHashSet();
        return candidates
            .Where(c => !taken.Contains(c.Space.Id) && _bookingManager.FindWindowProblem(c, fromUtc, toUtc, allowPast: true) == null)
            .Select(c => c.Space)
            .ToList();
    }

    /* Each space with its floor and building, for the per-space booking rules (one query each). */
    private async Task<List<SpaceContext>> ContextsOfAsync(List<Space> spaces)
    {
        var buildingIds = spaces.Select(s => s.BuildingId).Distinct().ToList();
        var floorIds = spaces.Select(s => s.FloorId).Distinct().ToList();
        var buildings = (await _buildings.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);
        return spaces.Select(s => new SpaceContext(s, floors.GetValueOrDefault(s.FloorId), buildings[s.BuildingId])).ToList();
    }

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

    /* Building, floor, type and upcoming-booking count for every space on the list (one query each), then the
     * rules the space actually follows and whether it can be booked. Names are in the reader's language. */
    protected override async Task<List<SpaceDto>> MapToGetListOutputDtosAsync(List<Space> entities)
    {
        var typeIds = entities.Select(s => s.TypeId).Distinct().ToList();
        var typeNames = (await _types.GetListAsync(t => typeIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.GetName());
        var upcoming = await CountUpcomingBookingsAsync(entities.Select(s => s.Id).ToList());

        return (await ContextsOfAsync(entities)).Select(ctx =>
        {
            var (s, floor, building) = ctx;
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
            dto.UpcomingBookingCount = upcoming.GetValueOrDefault(s.Id);
            return dto;
        }).ToList();
    }

    /* Only for the spaces being returned, in one grouped query. */
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
