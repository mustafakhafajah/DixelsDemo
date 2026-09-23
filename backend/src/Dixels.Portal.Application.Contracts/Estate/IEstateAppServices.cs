using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Estate;

public interface ITeamAppService : IApplicationService
{
    Task<ListResultDto<TeamDto>> GetListAsync();
    Task<TeamDto> CreateAsync(CreateUpdateTeamDto input);
    Task<TeamDto> UpdateAsync(Guid id, CreateUpdateTeamDto input);
    Task DeleteAsync(Guid id);
}

public interface IBuildingAppService : IApplicationService
{
    Task<ListResultDto<BuildingDto>> GetListAsync();
    Task<BuildingDto> GetAsync(Guid id);
    Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input);
    Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input);
    Task<BuildingDto> SetStatusAsync(Guid id, SetStatusDto input);
}

public interface IFloorAppService : IApplicationService
{
    Task<ListResultDto<FloorDto>> GetListAsync(Guid? buildingId);
    Task<FloorDto> CreateAsync(CreateUpdateFloorDto input);
    Task<FloorDto> UpdateAsync(Guid id, CreateUpdateFloorDto input);
    Task<FloorDto> SetStatusAsync(Guid id, SetStatusDto input);
}

public interface ISpaceAppService : IApplicationService
{
    Task<ListResultDto<SpaceDto>> GetListAsync();
    Task<SpaceDto> GetAsync(Guid id);
    Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input);
    Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input);
    Task<SpaceDto> SetStatusAsync(Guid id, SetStatusDto input);
}

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

public interface IActivityLogAppService : IApplicationService
{
    Task<ListResultDto<ActivityLogEntryDto>> GetListAsync(ActivityLogFilterDto input);
    Task RecordSignInAsync();
}

public interface IProfileLookupAppService : IApplicationService
{
    Task<CurrentUserProfileDto> GetCurrentAsync();
    Task<ListResultDto<UserLookupDto>> GetUsersAsync();
}
