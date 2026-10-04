using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Bookings;

/* A repeating booking: one series, many bookings sharing its id. */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/booking-series")]
public class BookingSeriesController : PortalController
{
    private readonly IBookingAppService _bookings;

    public BookingSeriesController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpPost]
    public Task<CreateBookingSeriesResultDto> CreateAsync([FromBody] CreateBookingSeriesDto input) => _bookings.CreateSeriesAsync(input);

    /* { lifecycle: "cancelled", fromUtc }: cancel the series' bookings from that moment on. */
    [HttpPatch("{seriesId}")]
    public Task<CancelSeriesResultDto> UpdateAsync(Guid seriesId, [FromBody] CancelBookingSeriesDto input)
        => _bookings.CancelSeriesAsync(seriesId, input);
}
