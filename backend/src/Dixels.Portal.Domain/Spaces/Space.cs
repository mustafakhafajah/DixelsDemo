using System;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Spaces;

/* No time zone of its own: a space is always in its building's time zone. */
public class Space : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public Guid TypeId { get; set; }
    /* Ticked = people may book it. A space is only bookable if its floor and building are too. */
    public bool IsBookable { get; set; } = true;
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public int Capacity { get; set; }
    public string? Note { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }

    protected Space() { }

    public Space(Guid id, string name, Guid buildingId, Guid floorId, Guid typeId) : base(id)
    {
        Name = name;
        BuildingId = buildingId;
        FloorId = floorId;
        TypeId = typeId;
    }
}
