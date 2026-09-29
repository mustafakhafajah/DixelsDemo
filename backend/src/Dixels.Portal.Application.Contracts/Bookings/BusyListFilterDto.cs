using System;

namespace Dixels.Portal.Bookings;

public class BusyListFilterDto
{
    public Guid? SpaceId { get; set; }
    /* Only items on spaces of this building / floor. */
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
