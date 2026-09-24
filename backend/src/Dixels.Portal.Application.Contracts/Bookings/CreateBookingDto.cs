using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

public class CreateBookingDto
{
    [Required] public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool Parking { get; set; }
    [StringLength(BookingConsts.MaxIdempotencyKeyLength)]
    public string? IdempotencyKey { get; set; }
}
