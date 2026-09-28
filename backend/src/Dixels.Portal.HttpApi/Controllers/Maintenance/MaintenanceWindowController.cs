using System;
using System.Threading.Tasks;
using Dixels.Portal.Maintenance;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Maintenance;

/* Blocked time. */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/maintenance-window")]
public class MaintenanceWindowController : PortalController, IMaintenanceWindowAppService
{
    private readonly IMaintenanceWindowAppService _maintenance;

    public MaintenanceWindowController(IMaintenanceWindowAppService maintenance)
    {
        _maintenance = maintenance;
    }

    [HttpGet]
    public Task<ListResultDto<MaintenanceWindowDto>> GetListAsync([FromQuery] MaintenanceListFilterDto input) => _maintenance.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<MaintenanceWindowDto> GetAsync(Guid id) => _maintenance.GetAsync(id);

    [HttpPost("preview-affected-bookings")]
    public Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync([FromBody] PreviewMaintenanceDto input)
        => _maintenance.PreviewAffectedBookingsAsync(input);

    [HttpPost("schedule")]
    public Task<ScheduleMaintenanceResultDto> ScheduleAsync([FromBody] ScheduleMaintenanceDto input) => _maintenance.ScheduleAsync(input);

    [HttpPost("{id}/cancel")]
    public Task<MaintenanceWindowDto> CancelAsync(Guid id) => _maintenance.CancelAsync(id);
}
