using System;

namespace Dixels.Portal.Bookings;

public class BookingWindowFailureDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ErrorCode { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
}
