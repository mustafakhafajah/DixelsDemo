using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Bookings;

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

    /* Internal: new bookings come from BookingManager.CreateAsync, which runs the booking rules first. */
    internal Booking(Guid id, Guid spaceId, Guid ownerUserId, DateTime startUtc, DateTime endUtc) : base(id)
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
