using System;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Floors;

public class FloorDto : EntityDto<Guid>
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public EstateStatus Status { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }
    public int SpaceCount { get; set; }
}
