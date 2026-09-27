using System;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Spaces;

public class SpaceDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid TypeId { get; set; }
    public string TypeName { get; set; } = null!;
    public bool IsBookable { get; set; } = true;
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = null!;
    public Guid FloorId { get; set; }
    public string FloorName { get; set; } = null!;
    /* Always the building's time zone. */
    public string TimeZone { get; set; } = null!;
    public int Capacity { get; set; }
    public string? Note { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }
    public ResolvedConstraintsDto Constraints { get; set; } = new();
    public bool CanCurrentUserBook { get; set; }
    /* Why it cannot be booked right now (the space, its floor or its building is not bookable); null when it can. */
    public string? NotBookableReason { get; set; }
}
