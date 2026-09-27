using System;
using System.Threading.Tasks;
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
}
