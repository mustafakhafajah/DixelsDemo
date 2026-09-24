using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Estate;

public interface IBookingAppService : IApplicationService
{
    Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input);
    Task<BookingDto> GetAsync(Guid id);
    Task<BookingDto> CreateAsync(CreateBookingDto input);
    Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input);
    Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input);
    Task<BookingDto> CancelAsync(Guid id);
    Task<CancelSeriesResultDto> CancelSeriesFromAsync(Guid id);
    Task<BookingDto> EndEarlyAsync(Guid id);
}

public interface IMaintenanceWindowAppService : IApplicationService
{
    Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input);
    Task<MaintenanceWindowDto> GetAsync(Guid id);
    Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync(PreviewMaintenanceDto input);
    Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input);
    Task<MaintenanceWindowDto> CancelAsync(Guid id);
}

public interface IProfileLookupAppService : IApplicationService
{
    Task<CurrentUserProfileDto> GetCurrentAsync();
    Task<ListResultDto<UserLookupDto>> GetUsersAsync();
}
