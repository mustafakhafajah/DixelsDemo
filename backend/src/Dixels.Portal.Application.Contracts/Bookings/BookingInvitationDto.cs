using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Bookings;

/* What an outside guest sees on the public answer page: enough to know which booking it is and their answer,
 * nothing about anyone else on it. */
public class BookingInvitationDto
{
    public string SpaceName { get; set; } = null!;
    /* In the request's language; the page writes them out ("HQ North · Floor 3"). FloorName is null without a floor. */
    public string BuildingName { get; set; } = null!;
    public string? FloorName { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    /* The building's time zone, so the page can add "= 14:30 Warsaw time" next to the guest's own time. */
    public string TimeZone { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    /* "none", "accepted", "tentative" or "declined". */
    public string Response { get; set; } = null!;
    /* "scheduled", "in_progress", "ended" or "cancelled": only an open booking can still be answered. */
    public string Lifecycle { get; set; } = null!;
}

/* For outside guests, who can't sign in: their invitation's Accept / Tentative / Decline buttons carry a private
 * secret, which is all these need. */
public interface IBookingInvitationAppService : IApplicationService
{
    Task<BookingInvitationDto> GetAsync(string token);
    /* Records the answer (WholeSeries: also for the series' later dates they are invited to); the owner is told. */
    Task<BookingInvitationDto> RespondAsync(string token, RespondToBookingDto input);
}
