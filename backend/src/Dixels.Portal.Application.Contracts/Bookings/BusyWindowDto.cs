using System;

namespace Dixels.Portal.Bookings;

/* "This space is taken from .. to .." and nothing else: no booking id, owner or name.
 * What an employee is told about other people's bookings. */
public class BusyWindowDto
{
    public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}
