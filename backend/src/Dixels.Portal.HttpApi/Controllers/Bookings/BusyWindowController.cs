using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Bookings;

/* Other people's confirmed bookings as bare "taken from .. to .." windows (no id, owner or name). */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/busy-windows")]
public class BusyWindowController : PortalController
{
    private readonly IBookingAppService _bookings;

    public BusyWindowController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public Task<ListResultDto<BusyWindowDto>> GetListAsync([FromQuery] BusyListFilterDto input) => _bookings.GetBusyListAsync(input);
}
