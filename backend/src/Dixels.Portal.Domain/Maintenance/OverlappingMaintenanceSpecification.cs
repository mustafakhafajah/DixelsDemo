using System;
using System.Linq.Expressions;
using Volo.Abp.Specifications;

namespace Dixels.Portal.Maintenance;

/* Active maintenance windows that overlap the half-open window [start, end), same rule as bookings. */
public class OverlappingMaintenanceSpecification : Specification<MaintenanceWindow>
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public OverlappingMaintenanceSpecification(DateTime startUtc, DateTime endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public override Expression<Func<MaintenanceWindow, bool>> ToExpression()
    {
        var start = StartUtc;
        var end = EndUtc;
        return m => m.Status == MaintenanceStatus.Active && m.StartUtc < end && start < m.EndUtc;
    }
}
