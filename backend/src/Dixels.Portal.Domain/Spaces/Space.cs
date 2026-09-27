using System;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Spaces;

public class Space : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public SpaceType Type { get; set; }
    public EstateStatus Status { get; set; }
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public int Capacity { get; set; }
    public string? Note { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }

    protected Space() { }

    public Space(Guid id, string name, Guid buildingId, Guid floorId) : base(id)
    {
        Name = name;
        BuildingId = buildingId;
        FloorId = floorId;
    }
}
