using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Bookings;

public class CreateBookingSeriesDto
{
    [Required] public Guid SpaceId { get; set; }
    [Required, MinLength(1), MaxLength(EstateConsts.MaxRecurrenceOccurrences)]
    public List<TimeWindowDto> Occurrences { get; set; } = new();
    public bool Parking { get; set; }
}
