using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Buildings;

public class BuildingDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string TimeZone { get; set; } = null!;
    public EstateStatus Status { get; set; }
    public int OpenHour { get; set; }
    public int CloseHour { get; set; }
    public int MinBookingMinutes { get; set; }
    public int MaxBookingHours { get; set; }
    public List<DateOnly> Holidays { get; set; } = new();
    public int FloorCount { get; set; }
    public int SpaceCount { get; set; }
}
