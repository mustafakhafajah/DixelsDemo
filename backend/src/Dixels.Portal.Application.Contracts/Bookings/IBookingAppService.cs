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
    /* Other people's confirmed bookings as bare "taken from .. to .." windows (no id, owner or name). */
    Task<ListResultDto<BusyWindowDto>> GetBusyListAsync(BusyListFilterDto input);
    Task<BookingDto> CreateAsync(CreateBookingDto input);
    Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input);
    /* PATCH: a new time, or a new lifecycle state ("cancelled" / "ended"); each goes to the matching action below. */
    Task<BookingDto> UpdateAsync(Guid id, UpdateBookingDto input);
    Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input);
    Task<BookingDto> CancelAsync(Guid id);
    /* Cancels the series' bookings that start at or after input.FromUtc; ended occurrences are left alone. */
    Task<CancelSeriesResultDto> CancelSeriesAsync(Guid seriesId, CancelBookingSeriesDto input);
    Task<BookingDto> EndEarlyAsync(Guid id);
    /* Admin: bookings in a space, floor or building that have not started yet (confirmed only). */
    Task<int> GetUpcomingCountAsync(EstateScopeDto input);
    /* Admin: cancel exactly those bookings, e.g. after making the scope not bookable. */
    Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input);
    /* The owner replaces who is invited; added people get an invitation, removed ones are told. */
    Task<BookingDto> SetAttendeesAsync(Guid id, SetBookingAttendeesDto input);
    /* An invited user takes themselves off the booking (and, with wholeSeries, off its later dates too);
     * the owner is told. */
    Task LeaveAsync(Guid id, bool wholeSeries = false);
    /* Active users to invite, by name, user name or email. */
    Task<ListResultDto<BookingPersonDto>> GetPeopleAsync(BookingPeopleFilterDto input);
}
