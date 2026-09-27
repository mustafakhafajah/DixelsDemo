using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings.Queries;

public record GetBuildingListQuery : IQuery<ListResultDto<BuildingDto>>;

public class GetBuildingListQueryHandler : IQueryHandler<GetBuildingListQuery, ListResultDto<BuildingDto>>
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingDtoMapper _mapper;

    public GetBuildingListQueryHandler(IRepository<Building, Guid> buildings, BuildingDtoMapper mapper)
    {
        _buildings = buildings;
        _mapper = mapper;
    }

    public async Task<ListResultDto<BuildingDto>> HandleAsync(GetBuildingListQuery query)
    {
        var buildings = await _buildings.GetListAsync();
        return new ListResultDto<BuildingDto>(await _mapper.MapListAsync(buildings.OrderBy(b => b.Name)));
    }
}
