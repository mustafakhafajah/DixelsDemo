using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

/* PUT /api/app/bookings/{id}/attendees/me/response: the signed-in person answers an invitation, as in Teams.
 * Response is "accepted", "tentative" or "declined"; WholeSeries also answers the series' later dates that are
 * still to come and that they are invited to. */
public class RespondToBookingDto
{
    [StringLength(16)]
    public string? Response { get; set; }

    public bool WholeSeries { get; set; }
}
