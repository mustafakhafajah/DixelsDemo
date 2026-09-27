using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors.Queries;

/* BuildingId null means every floor in every building. */
public record GetFloorListQuery(Guid? BuildingId) : IQuery<ListResultDto<FloorDto>>;

public class GetFloorListQueryHandler : IQueryHandler<GetFloorListQuery, ListResultDto<FloorDto>>
{
    private readonly IRepository<Floor, Guid> _floors;
    private readonly FloorDtoMapper _mapper;

    public GetFloorListQueryHandler(IRepository<Floor, Guid> floors, FloorDtoMapper mapper)
    {
        _floors = floors;
        _mapper = mapper;
    }

    public async Task<ListResultDto<FloorDto>> HandleAsync(GetFloorListQuery query)
    {
        var floors = query.BuildingId.HasValue
            ? await _floors.GetListAsync(f => f.BuildingId == query.BuildingId.Value)
            : await _floors.GetListAsync();
        var dtos = await _mapper.MapListAsync(floors);
        return new ListResultDto<FloorDto>(dtos
            .OrderBy(f => f.BuildingName)
            .ThenBy(f => f.Name.PadLeft(8, '0'))
            .ToList());
    }
}
