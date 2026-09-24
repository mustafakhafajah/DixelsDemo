using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Estate;

public class Booking : FullAuditedAggregateRoot<Guid>
{
    public Guid SpaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SeriesId { get; set; }
    public bool Parking { get; set; }
    public string? IdempotencyKey { get; set; }

    protected Booking() { }

    public Booking(Guid id, Guid spaceId, Guid ownerUserId, DateTime startUtc, DateTime endUtc) : base(id)
    {
        SpaceId = spaceId;
        OwnerUserId = ownerUserId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = BookingStatus.Confirmed;
    }

    public string GetLifecycle(DateTime nowUtc)
    {
        if (Status == BookingStatus.Cancelled) return "cancelled";
        if (EndUtc <= nowUtc) return "ended";
        if (StartUtc <= nowUtc) return "in_progress";
        return "scheduled";
    }
}

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
    {
        if (Status == MaintenanceStatus.Cancelled) return "cancelled";
        if (EndUtc <= nowUtc) return "ended";
        if (StartUtc <= nowUtc) return "in_progress";
        return "scheduled";
    }
}
