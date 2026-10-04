using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Estate;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Bookings;

/* /api/app/bookings. Series, busy windows and a scope's upcoming bookings have their own resources
 * (BookingSeriesController, BusyWindowController, UpcomingBookingsController). */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/bookings")]
public class BookingController : PortalController, IBookingAppService
{
    private readonly IBookingAppService _bookings;

    public BookingController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public Task<ListResultDto<BookingDto>> GetListAsync([FromQuery] BookingListFilterDto input) => _bookings.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<BookingDto> GetAsync(Guid id) => _bookings.GetAsync(id);

    [HttpPost]
    public Task<BookingDto> CreateAsync([FromBody] CreateBookingDto input) => _bookings.CreateAsync(input);

    /* { startUtc, endUtc, expectedVersion } moves it; { lifecycle: "cancelled" } cancels it;
     * { lifecycle: "ended" } ends it now. A cancelled booking is kept, so cancelling is not a DELETE. */
    [HttpPatch("{id}")]
    public Task<BookingDto> UpdateAsync(Guid id, [FromBody] UpdateBookingDto input) => _bookings.UpdateAsync(id, input);

    /* Reached through PATCH above. */
    [NonAction]
    public Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input) => _bookings.RescheduleAsync(id, input);

    [NonAction]
    public Task<BookingDto> CancelAsync(Guid id) => _bookings.CancelAsync(id);

    [NonAction]
    public Task<BookingDto> EndEarlyAsync(Guid id) => _bookings.EndEarlyAsync(id);

    /* Served by their own resources. */
    [NonAction]
    public Task<ListResultDto<BusyWindowDto>> GetBusyListAsync(BusyListFilterDto input) => _bookings.GetBusyListAsync(input);

    [NonAction]
    public Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input) => _bookings.CreateSeriesAsync(input);

    [NonAction]
    public Task<CancelSeriesResultDto> CancelSeriesAsync(Guid seriesId, CancelBookingSeriesDto input) => _bookings.CancelSeriesAsync(seriesId, input);

    [NonAction]
    public Task<int> GetUpcomingCountAsync(EstateScopeDto input) => _bookings.GetUpcomingCountAsync(input);

    [NonAction]
    public Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input) => _bookings.CancelUpcomingAsync(input);
}
