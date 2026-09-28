using System;

namespace Dixels.Portal.Bookings;

public class BusyListFilterDto
{
    public Guid? SpaceId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
