using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Buildings;

public class Building : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public string TimeZone { get; set; } = "UTC";
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public int OpenHour { get; set; } = BuildingConsts.DefaultOpenHour;
    public int CloseHour { get; set; } = BuildingConsts.DefaultCloseHour;
    public int MinBookingMinutes { get; set; } = BuildingConsts.DefaultMinBookingMinutes;
    public int MaxBookingHours { get; set; } = BuildingConsts.DefaultMaxBookingHours;
    public List<DateOnly> Holidays { get; set; } = new();

    protected Building() { }

    public Building(Guid id, string name) : base(id)
    {
        Name = name;
    }
}
