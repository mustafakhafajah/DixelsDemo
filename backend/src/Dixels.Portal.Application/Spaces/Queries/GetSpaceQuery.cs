using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces.Queries;

public record GetSpaceQuery(Guid Id) : IQuery<SpaceDto>;

public class GetSpaceQueryHandler : IQueryHandler<GetSpaceQuery, SpaceDto>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceDtoMapper _mapper;

    public GetSpaceQueryHandler(IRepository<Space, Guid> spaces, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _mapper = mapper;
    }

    public async Task<SpaceDto> HandleAsync(GetSpaceQuery query)
    {
        var space = await _spaces.FindAsync(query.Id)
            ?? throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "No space with that ID.");
        return await _mapper.MapAsync(space);
    }
}
