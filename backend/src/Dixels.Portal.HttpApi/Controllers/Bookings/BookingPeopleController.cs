using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Bookings;

/* Active people to invite to a booking (?filter= name, user name or email); at most 20, never yourself. */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/booking-people")]
public class BookingPeopleController : PortalController
{
    private readonly IBookingAppService _bookings;

    public BookingPeopleController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public Task<ListResultDto<BookingPersonDto>> GetListAsync([FromQuery] BookingPeopleFilterDto input) => _bookings.GetPeopleAsync(input);
}
