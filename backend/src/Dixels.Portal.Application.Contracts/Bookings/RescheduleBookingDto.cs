using System;

namespace Dixels.Portal.Bookings;

public class RescheduleBookingDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int? ExpectedVersion { get; set; }
}
