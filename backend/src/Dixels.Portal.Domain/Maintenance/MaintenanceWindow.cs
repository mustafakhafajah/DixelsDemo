using System;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Maintenance;

public class MaintenanceWindow : FullAuditedAggregateRoot<Guid>
{
    public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Note { get; set; }
    public Guid? SeriesId { get; set; }
    /* Provenance only: what the admin originally targeted. SpaceId is authoritative. */
    public MaintenanceScopeType ScopeType { get; set; }
    public Guid ScopeId { get; set; }
    public MaintenanceStatus Status { get; set; }

    protected MaintenanceWindow() { }

    public MaintenanceWindow(Guid id, Guid spaceId, DateTime startUtc, DateTime endUtc) : base(id)
    {
        SpaceId = spaceId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = MaintenanceStatus.Active;
    }

    public string GetLifecycle(DateTime nowUtc)
        => TimeWindowLifecycle.Get(Status == MaintenanceStatus.Cancelled, StartUtc, EndUtc, nowUtc);
}
