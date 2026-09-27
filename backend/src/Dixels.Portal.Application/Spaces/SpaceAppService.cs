using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces.Commands;
using Dixels.Portal.Spaces.Queries;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Spaces;

[Authorize]
public class SpaceAppService : PortalAppService, ISpaceAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public SpaceAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<SpaceDto>> GetListAsync()
        => _queries.QueryAsync(new GetSpaceListQuery());

    public Task<ListResultDto<SpaceDto>> GetBookableListAsync(FindSpacesInput input)
        => _queries.QueryAsync(new GetBookableSpacesQuery(input));

    public Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input)
        => _queries.QueryAsync(new GetSpaceRegistryPageQuery(input));

    public Task<SpaceDto> GetAsync(Guid id)
        => _queries.QueryAsync(new GetSpaceQuery(id));

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
        => _commands.SendAsync(new CreateSpaceCommand(input));

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input)
        => _commands.SendAsync(new UpdateSpaceCommand(id, input));

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input)
        => _commands.SendAsync(new SetSpaceBookableCommand(id, input.IsBookable));
}
