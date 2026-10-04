using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Maintenance;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Maintenance;

/* Blocked time. */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/maintenance-windows")]
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

    /* Block time: one window per occurrence. */
    [HttpPost]
    public Task<ScheduleMaintenanceResultDto> ScheduleAsync([FromBody] ScheduleMaintenanceDto input) => _maintenance.ScheduleAsync(input);

    /* What blocking this time would affect, worked out from the submitted windows; nothing is saved. */
    [HttpPost("previews")]
    public Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync([FromBody] PreviewMaintenanceDto input)
        => _maintenance.PreviewAffectedBookingsAsync(input);

    /* { lifecycle: "cancelled" } unblocks it. A cancelled window is kept, so this is not a DELETE. */
    [HttpPatch("{id}")]
    public Task<MaintenanceWindowDto> UpdateAsync(Guid id, [FromBody] CancellationDto input) => _maintenance.CancelAsync(id);

    /* Reached through PATCH above. */
    [NonAction]
    public Task<MaintenanceWindowDto> CancelAsync(Guid id) => _maintenance.CancelAsync(id);
}
