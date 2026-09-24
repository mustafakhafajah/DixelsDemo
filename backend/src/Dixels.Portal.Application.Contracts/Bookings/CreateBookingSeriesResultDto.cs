using System;
using System.Collections.Generic;

namespace Dixels.Portal.Bookings;

public class CreateBookingSeriesResultDto
{
    public Guid? SeriesId { get; set; }
    public List<BookingDto> Created { get; set; } = new();
    public List<BookingWindowFailureDto> Skipped { get; set; } = new();
}
