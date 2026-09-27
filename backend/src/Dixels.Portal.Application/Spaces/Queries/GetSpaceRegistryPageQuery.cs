using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace Dixels.Portal.Spaces.Queries;

/* One filtered page for the admin registry, which must scale to thousands of spaces. */
public record GetSpaceRegistryPageQuery(GetSpacesInput Input) : IQuery<SpaceRegistryPageDto>;

public class GetSpaceRegistryPageQueryHandler : IQueryHandler<GetSpaceRegistryPageQuery, SpaceRegistryPageDto>
{
    /* A registry page never shows more than this many rows, whatever the client asks for. */
    private const int MaxPageSize = 100;

    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly SpaceDtoMapper _mapper;
    private readonly IAsyncQueryableExecuter _async;
    private readonly IClock _clock;

    public GetSpaceRegistryPageQueryHandler(IRepository<Space, Guid> spaces, IRepository<Building, Guid> buildings,
        IRepository<Booking, Guid> bookings, SpaceDtoMapper mapper, IAsyncQueryableExecuter async, IClock clock)
    {
        _spaces = spaces;
        _buildings = buildings;
        _bookings = bookings;
        _mapper = mapper;
        _async = async;
        _clock = clock;
    }

    public async Task<SpaceRegistryPageDto> HandleAsync(GetSpaceRegistryPageQuery query)
    {
        var input = query.Input;
        var filtered = (await _spaces.GetQueryableAsync()).ApplyRegistryFilter(input);
        var total = await _async.CountAsync(filtered);

        /* Same order as the full list: building name, then space name (Id breaks ties so pages never overlap). */
        var ordered = from s in filtered
                      join b in await _buildings.GetQueryableAsync() on s.BuildingId equals b.Id
                      orderby b.Name, s.Name, s.Id
                      select s;
        var take = Math.Clamp(input.MaxResultCount, 1, MaxPageSize);
        var page = await _async.ToListAsync(ordered.Skip(Math.Max(0, input.SkipCount)).Take(take));

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
        var now = _clock.Now;
        var counts = await _async.ToListAsync((await _bookings.GetQueryableAsync())
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.EndUtc > now)
            .GroupBy(b => b.SpaceId)
            .Select(g => new { SpaceId = g.Key, Count = g.Count() }));
        return counts.ToDictionary(c => c.SpaceId, c => c.Count);
    }
}
