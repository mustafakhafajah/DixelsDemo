using System;

namespace Dixels.Portal.Bookings;

public class BookingListFilterDto
{
    public Guid? SpaceId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool IncludeCancelled { get; set; }
}
