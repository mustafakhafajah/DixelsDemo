using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Estate;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Bookings;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/booking")]
public class BookingController : PortalController, IBookingAppService
{
    private readonly IBookingAppService _bookings;

    public BookingController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public Task<ListResultDto<BookingDto>> GetListAsync([FromQuery] BookingListFilterDto input) => _bookings.GetListAsync(input);

    /* Admin: how many upcoming bookings a space, floor or building has. */
    [HttpGet("upcoming-count")]
    public Task<int> GetUpcomingCountAsync([FromQuery] EstateScopeDto input) => _bookings.GetUpcomingCountAsync(input);

    [HttpGet("{id}")]
    public Task<BookingDto> GetAsync(Guid id) => _bookings.GetAsync(id);

    [HttpPost]
    public Task<BookingDto> CreateAsync([FromBody] CreateBookingDto input) => _bookings.CreateAsync(input);

    [HttpPost("series")]
    public Task<CreateBookingSeriesResultDto> CreateSeriesAsync([FromBody] CreateBookingSeriesDto input) => _bookings.CreateSeriesAsync(input);

    /* Admin: cancel every upcoming booking in a space, floor or building. */
    [HttpPost("cancel-upcoming")]
    public Task<CancelUpcomingResultDto> CancelUpcomingAsync([FromBody] EstateScopeDto input) => _bookings.CancelUpcomingAsync(input);

    [HttpPost("{id}/reschedule")]
    public Task<BookingDto> RescheduleAsync(Guid id, [FromBody] RescheduleBookingDto input) => _bookings.RescheduleAsync(id, input);

    [HttpPost("{id}/cancel")]
    public Task<BookingDto> CancelAsync(Guid id) => _bookings.CancelAsync(id);

    [HttpPost("{id}/cancel-series-from")]
    public Task<CancelSeriesResultDto> CancelSeriesFromAsync(Guid id) => _bookings.CancelSeriesFromAsync(id);

    [HttpPost("{id}/end-early")]
    public Task<BookingDto> EndEarlyAsync(Guid id) => _bookings.EndEarlyAsync(id);
}
