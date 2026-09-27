using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings.Commands;
using Dixels.Portal.Buildings.Queries;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Buildings;

/* The HTTP boundary: permissions live here, the work lives in one handler per use case. */
[Authorize]
public class BuildingAppService : PortalAppService, IBuildingAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public BuildingAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<BuildingDto>> GetListAsync()
        => _queries.QueryAsync(new GetBuildingListQuery());

    public Task<BuildingDto> GetAsync(Guid id)
        => _queries.QueryAsync(new GetBuildingQuery(id));

    [Authorize(PortalPermissions.Buildings.Manage)]
    public Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input)
        => _commands.SendAsync(new CreateBuildingCommand(input));

    [Authorize(PortalPermissions.Buildings.Manage)]
    public Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input)
        => _commands.SendAsync(new UpdateBuildingCommand(id, input));

    [Authorize(PortalPermissions.Buildings.Manage)]
    public Task<BuildingDto> SetBookableAsync(Guid id, SetBookableDto input)
        => _commands.SendAsync(new SetBuildingBookableCommand(id, input.IsBookable));
}
