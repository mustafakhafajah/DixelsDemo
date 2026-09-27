using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings.Queries;

public record GetBuildingQuery(Guid Id) : IQuery<BuildingDto>;

public class GetBuildingQueryHandler : IQueryHandler<GetBuildingQuery, BuildingDto>
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingDtoMapper _mapper;

    public GetBuildingQueryHandler(IRepository<Building, Guid> buildings, BuildingDtoMapper mapper)
    {
        _buildings = buildings;
        _mapper = mapper;
    }

    public async Task<BuildingDto> HandleAsync(GetBuildingQuery query)
        => await _mapper.MapAsync(await _buildings.GetAsync(query.Id));
}
