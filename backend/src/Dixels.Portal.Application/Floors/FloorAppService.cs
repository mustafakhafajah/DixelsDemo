using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors.Commands;
using Dixels.Portal.Floors.Queries;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Floors;

[Authorize]
public class FloorAppService : PortalAppService, IFloorAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public FloorAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<FloorDto>> GetListAsync(Guid? buildingId)
        => _queries.QueryAsync(new GetFloorListQuery(buildingId));

    [Authorize(PortalPermissions.Floors.Manage)]
    public Task<FloorDto> CreateAsync(CreateUpdateFloorDto input)
        => _commands.SendAsync(new CreateFloorCommand(input));

    [Authorize(PortalPermissions.Floors.Manage)]
    public Task<FloorDto> UpdateAsync(Guid id, CreateUpdateFloorDto input)
        => _commands.SendAsync(new UpdateFloorCommand(id, input));

    [Authorize(PortalPermissions.Floors.Manage)]
    public Task<FloorDto> SetBookableAsync(Guid id, SetBookableDto input)
        => _commands.SendAsync(new SetFloorBookableCommand(id, input.IsBookable));
}
