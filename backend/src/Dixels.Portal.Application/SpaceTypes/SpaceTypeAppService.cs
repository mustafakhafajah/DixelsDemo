using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Permissions;
using Dixels.Portal.SpaceTypes.Commands;
using Dixels.Portal.SpaceTypes.Queries;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.SpaceTypes;

/* Space types are part of managing spaces, so they use the same permission. */
[Authorize]
public class SpaceTypeAppService : PortalAppService, ISpaceTypeAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public SpaceTypeAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<SpaceTypeDto>> GetListAsync()
        => _queries.QueryAsync(new GetSpaceTypeListQuery());

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task<SpaceTypeDto> CreateAsync(CreateUpdateSpaceTypeDto input)
        => _commands.SendAsync(new CreateSpaceTypeCommand(input));

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task<SpaceTypeDto> UpdateAsync(Guid id, CreateUpdateSpaceTypeDto input)
        => _commands.SendAsync(new UpdateSpaceTypeCommand(id, input));

    [Authorize(PortalPermissions.Spaces.Manage)]
    public Task DeleteAsync(Guid id)
        => _commands.SendAsync(new DeleteSpaceTypeCommand(id));
}
