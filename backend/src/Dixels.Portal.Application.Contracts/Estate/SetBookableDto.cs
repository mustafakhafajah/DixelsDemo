namespace Dixels.Portal.Estate;

/* Shared by buildings, floors and spaces: tick or untick "Bookable". Existing bookings are kept;
 * cancelling them is a separate, explicit admin action (see IBookingAppService.CancelUpcomingAsync). */
public class SetBookableDto
{
    public bool IsBookable { get; set; }
}
