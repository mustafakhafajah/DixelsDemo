using System;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Bookings;

/* PATCH /api/app/booking-series/{seriesId}: cancel the series' bookings that start at or after FromUtc.
 * Ended occurrences are left alone. */
public class CancelBookingSeriesDto : CancellationDto
{
    public DateTime FromUtc { get; set; }
}
