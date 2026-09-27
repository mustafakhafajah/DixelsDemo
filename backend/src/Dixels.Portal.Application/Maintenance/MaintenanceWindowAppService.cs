using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Maintenance.Commands;
using Dixels.Portal.Maintenance.Queries;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Maintenance;

[Authorize]
public class MaintenanceWindowAppService : PortalAppService, IMaintenanceWindowAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public MaintenanceWindowAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input)
        => _queries.QueryAsync(new GetMaintenanceWindowListQuery(input));

    public Task<MaintenanceWindowDto> GetAsync(Guid id)
        => _queries.QueryAsync(new GetMaintenanceWindowQuery(id));

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync(PreviewMaintenanceDto input)
        => _queries.QueryAsync(new PreviewAffectedBookingsQuery(input));

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input)
        => _commands.SendAsync(new ScheduleMaintenanceCommand(input));

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public Task<MaintenanceWindowDto> CancelAsync(Guid id)
        => _commands.SendAsync(new CancelMaintenanceWindowCommand(id));
}
