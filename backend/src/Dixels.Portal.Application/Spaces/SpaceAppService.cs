using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Permissions;
using Dixels.Portal.SpaceTypes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

[Authorize]
public class SpaceAppService : EstateAppServiceBase, ISpaceAppService
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly SpaceDtoMapper _mapper;

    /* A registry page never shows more than this many rows, whatever the client asks for. */
    private const int MaxPageSize = 100;

    public SpaceAppService(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, IRepository<Booking, Guid> bookings, IRepository<SpaceType, Guid> types, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _bookings = bookings;
        _types = types;
        _mapper = mapper;
    }

    public async Task<ListResultDto<SpaceDto>> GetListAsync()
    {
        var dtos = await _mapper.MapListAsync(await _spaces.GetListAsync());
        return new ListResultDto<SpaceDto>(dtos.OrderBy(d => d.BuildingName).ThenBy(d => d.Name).ToList());
    }

    public async Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input)
    {
        var filtered = (await _spaces.GetQueryableAsync()).ApplyRegistryFilter(input);
        var total = await AsyncExecuter.CountAsync(filtered);

        /* Same order as the full list: building name, then space name (Id breaks ties so pages never overlap). */
        var ordered = from s in filtered
                      join b in await _buildings.GetQueryableAsync() on s.BuildingId equals b.Id
                      orderby b.Name, s.Name, s.Id
                      select s;
        var take = Math.Clamp(input.MaxResultCount, 1, MaxPageSize);
        var page = await AsyncExecuter.ToListAsync(ordered.Skip(Math.Max(0, input.SkipCount)).Take(take));

        return new SpaceRegistryPageDto
        {
            TotalCount = total,
            Items = await _mapper.MapListAsync(page),
            UpcomingBookingCounts = await CountUpcomingBookingsAsync(page.Select(s => s.Id).ToList()),
        };
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

    public async Task<SpaceDto> GetAsync(Guid id)
    {
        var space = await _spaces.FindAsync(id)
            ?? throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "No space with that ID.");
        return await _mapper.MapAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
    {
        var (name, building, floor) = await ValidateAsync(input, null);
        var space = new Space(GuidGenerator.Create(), name, building.Id, floor.Id, input.TypeId);
        Apply(space, input);
        await _spaces.InsertAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input)
    {
        var space = await _spaces.GetAsync(id);
        var (name, building, floor) = await ValidateAsync(input, id);
        space.Name = name;
        space.BuildingId = building.Id;
        space.FloorId = floor.Id;
        Apply(space, input);
        await _spaces.UpdateAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input)
    {
        var space = await _spaces.GetAsync(id);
        space.IsBookable = input.IsBookable;
        await _spaces.UpdateAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }

    private async Task<(string Name, Building Building, Floor Floor)> ValidateAsync(CreateUpdateSpaceDto input, Guid? excludeId)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space a name.");
        var building = await _buildings.FindAsync(input.BuildingId)
            ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building. Add one first if the list is empty.");
        var floor = await _floors.FindAsync(input.FloorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new BusinessException(PortalDomainErrorCodes.InvalidFloor, $"Pick a floor that belongs to {building.Name}.");
        var lower = name.ToLower();
        if (await _spaces.AnyAsync(s => s.Name.ToLower() == lower && s.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceDuplicateName, "Another space already uses that name.");
        if (!await _types.AnyAsync(t => t.Id == input.TypeId))
            throw new BusinessException(PortalDomainErrorCodes.InvalidSpaceType, "Pick a space type. Add one on the Space types page if none fits.");

        EstateOverrideRules.EnsureOnlyNarrows("Space", "its floor", "its floor's",
            ConstraintResolver.ResolveBounds(building, floor),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
        return (name, building, floor);
    }

    private static void Apply(Space s, CreateUpdateSpaceDto input)
    {
        s.TypeId = input.TypeId;
        s.IsBookable = input.IsBookable;
        s.Capacity = Math.Max(0, input.Capacity);
        s.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        s.OpenHourOverride = input.OpenHourOverride;
        s.CloseHourOverride = input.CloseHourOverride;
        s.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        s.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }
}
