using System;
using System.Linq.Expressions;
using Volo.Abp.Specifications;

namespace Dixels.Portal.Bookings;

/* Confirmed bookings that overlap the half-open window [start, end).
 * Half-open means touching is not overlapping: 09:00-10:00 and 10:00-11:00 can both exist. */
public class OverlappingBookingsSpecification : Specification<Booking>
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public OverlappingBookingsSpecification(DateTime startUtc, DateTime endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public override Expression<Func<Booking, bool>> ToExpression()
    {
        var start = StartUtc;
        var end = EndUtc;
        return b => b.Status == BookingStatus.Confirmed && b.StartUtc < end && start < b.EndUtc;
    }
}
