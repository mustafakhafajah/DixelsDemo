using System;

namespace Dixels.Portal.Bookings;

public class CancelSeriesResultDto
{
    public Guid? SeriesId { get; set; }
    public int CancelledCount { get; set; }
}
