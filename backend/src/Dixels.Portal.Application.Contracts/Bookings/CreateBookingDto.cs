using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

public class CreateBookingDto
{
    [Required] public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    [StringLength(BookingConsts.MaxIdempotencyKeyLength)]
    public string? IdempotencyKey { get; set; }
    [MaxLength(BookingConsts.MaxAttendees)]
    public List<AttendeeInputDto> Attendees { get; set; } = new();
}
