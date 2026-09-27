using System;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Floors;

public class Floor : FullAuditedAggregateRoot<Guid>
{
    public Guid BuildingId { get; set; }
    public string Name { get; set; } = null!;
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }

    protected Floor() { }

    public Floor(Guid id, Guid buildingId, string name) : base(id)
    {
        BuildingId = buildingId;
        Name = name;
    }
}
