using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Bookings;

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
    /* Admin: bookings in a space, floor or building that have not started yet (confirmed only). */
    Task<int> GetUpcomingCountAsync(EstateScopeDto input);
    /* Admin: cancel exactly those bookings, e.g. after making the scope not bookable. */
    Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input);
}
