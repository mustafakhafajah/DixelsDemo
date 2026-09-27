using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Maintenance;

public interface IMaintenanceWindowAppService : IApplicationService
{
    Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input);
    Task<MaintenanceWindowDto> GetAsync(Guid id);
    Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync(PreviewMaintenanceDto input);
    Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input);
    Task<MaintenanceWindowDto> CancelAsync(Guid id);
}
