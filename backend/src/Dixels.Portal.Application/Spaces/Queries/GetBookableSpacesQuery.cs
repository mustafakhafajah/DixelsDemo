using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Floors;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Dixels.Portal.Spaces.Queries;

/* "Find a space": bookable spaces matching the filters, filtered in the database so the page never has to
 * download every space in the estate. */
public record GetBookableSpacesQuery(FindSpacesInput Input) : IQuery<ListResultDto<SpaceDto>>;

public class GetBookableSpacesQueryHandler : IQueryHandler<GetBookableSpacesQuery, ListResultDto<SpaceDto>>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly SpaceDtoMapper _mapper;
    private readonly IAsyncQueryableExecuter _async;

    public GetBookableSpacesQueryHandler(IRepository<Space, Guid> spaces, IRepository<Building, Guid> buildings,
        IRepository<Floor, Guid> floors, SpaceDtoMapper mapper, IAsyncQueryableExecuter async)
    {
        _spaces = spaces;
        _buildings = buildings;
        _floors = floors;
        _mapper = mapper;
        _async = async;
    }

    public async Task<ListResultDto<SpaceDto>> HandleAsync(GetBookableSpacesQuery query)
    {
        var input = query.Input;
        var name = input.Name?.Trim().ToLower();
        var floorName = input.FloorName?.Trim();
        var minCapacity = input.MinCapacity ?? 0;
        var typeIds = input.TypeIds ?? new();

        /* Bookable = the building, the floor and the space are all bookable (same rule as BookingManager.CanBook;
         * a space whose floor no longer exists is judged by its building and itself). */
        var rows =
            from s in await _spaces.GetQueryableAsync()
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

        var spaces = await _async.ToListAsync(rows.OrderBy(r => r.BuildingName).ThenBy(r => r.Space.Name).Select(r => r.Space));
        return new ListResultDto<SpaceDto>(await _mapper.MapListAsync(spaces));
    }
}
