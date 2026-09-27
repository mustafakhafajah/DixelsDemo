using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Spaces;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes.Queries;

public record GetSpaceTypeListQuery : IQuery<ListResultDto<SpaceTypeDto>>;

public class GetSpaceTypeListQueryHandler : IQueryHandler<GetSpaceTypeListQuery, ListResultDto<SpaceTypeDto>>
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;

    public GetSpaceTypeListQueryHandler(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces)
    {
        _types = types;
        _spaces = spaces;
    }

    public async Task<ListResultDto<SpaceTypeDto>> HandleAsync(GetSpaceTypeListQuery query)
    {
        var types = await _types.GetListAsync();
        var counts = (await _spaces.GetListAsync()).GroupBy(s => s.TypeId).ToDictionary(g => g.Key, g => g.Count());
        return new ListResultDto<SpaceTypeDto>(types.OrderBy(t => t.Name)
            .Select(t => SpaceTypeDtoMapper.Map(t, counts.GetValueOrDefault(t.Id))).ToList());
    }
}
