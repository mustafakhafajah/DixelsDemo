using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Maintenance;

/* How blocking a time window affects existing bookings. The preview query only counts; the schedule
 * command counts and may cancel. Both must agree on what "affected" means, so it lives here once. */
public class MaintenanceBookingImpact : ITransientDependency
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IClock _clock;

    public MaintenanceBookingImpact(IRepository<Booking, Guid> bookings, IClock clock)
    {
        _bookings = bookings;
        _clock = clock;
    }

    public async Task<int> CountAffectedAsync(List<Guid> spaceIds, DateTime startUtc, DateTime endUtc)
        => await _bookings.CountAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId)));

    /* Only bookings that have not started: a meeting already in progress is never cut off. */
    public async Task<int> CancelAffectedAsync(List<Guid> spaceIds, DateTime startUtc, DateTime endUtc)
    {
        var now = _clock.Now;
        var hit = await _bookings.GetListAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId) && b.StartUtc > now));
        foreach (var b in hit) b.Cancel();
        await _bookings.UpdateManyAsync(hit, autoSave: true);
        return hit.Count;
    }
}
