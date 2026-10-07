using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Bookings;

/* /api/app/booking-invitations/{secret}: the public answer page of an outside guest (no sign-in; the private secret
 * from their invitation email is the key). */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/booking-invitations")]
public class BookingInvitationController : PortalController, IBookingInvitationAppService
{
    private readonly IBookingInvitationAppService _invitations;

    public BookingInvitationController(IBookingInvitationAppService invitations)
    {
        _invitations = invitations;
    }

    [HttpGet("{token}")]
    public Task<BookingInvitationDto> GetAsync(string token) => _invitations.GetAsync(token);

    /* { response: "accepted" | "tentative" | "declined", wholeSeries }. */
    [HttpPut("{token}/response")]
    public Task<BookingInvitationDto> RespondAsync(string token, [FromBody] RespondToBookingDto input) => _invitations.RespondAsync(token, input);
}
