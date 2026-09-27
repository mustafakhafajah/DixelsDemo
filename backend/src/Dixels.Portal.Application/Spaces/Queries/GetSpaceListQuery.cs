using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces.Queries;

/* Every space; used by pickers (booking modal, find page, schedule). */
public record GetSpaceListQuery : IQuery<ListResultDto<SpaceDto>>;

public class GetSpaceListQueryHandler : IQueryHandler<GetSpaceListQuery, ListResultDto<SpaceDto>>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceDtoMapper _mapper;

    public GetSpaceListQueryHandler(IRepository<Space, Guid> spaces, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _mapper = mapper;
    }

    public async Task<ListResultDto<SpaceDto>> HandleAsync(GetSpaceListQuery query)
    {
        var dtos = await _mapper.MapListAsync(await _spaces.GetListAsync());
        return new ListResultDto<SpaceDto>(dtos.OrderBy(d => d.BuildingName).ThenBy(d => d.Name).ToList());
    }
}
