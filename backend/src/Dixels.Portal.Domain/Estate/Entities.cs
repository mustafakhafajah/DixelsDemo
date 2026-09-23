using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.Estate;

public class Team : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;

    protected Team() { }

    public Team(Guid id, string name) : base(id)
    {
        Name = name;
    }
}

public class Building : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public string TimeZone { get; set; } = "UTC";
    public EstateStatus Status { get; set; }
    public int OpenHour { get; set; } = EstateConsts.DefaultOpenHour;
    public int CloseHour { get; set; } = EstateConsts.DefaultCloseHour;
    public int MinBookingMinutes { get; set; } = EstateConsts.DefaultMinBookingMinutes;
    public int MaxBookingHours { get; set; } = EstateConsts.DefaultMaxBookingHours;
    public List<DateOnly> Holidays { get; set; } = new();

    protected Building() { }

    public Building(Guid id, string name) : base(id)
    {
        Name = name;
    }
}

public class Floor : FullAuditedAggregateRoot<Guid>
{
    public Guid BuildingId { get; set; }
    public string Name { get; set; } = null!;
    public EstateStatus Status { get; set; }
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

public class Space : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public SpaceType Type { get; set; }
    public EstateStatus Status { get; set; }
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public int Capacity { get; set; }
    public List<Guid> RestrictedTeamIds { get; set; } = new();
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

public class Booking : FullAuditedAggregateRoot<Guid>
{
    public Guid SpaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SeriesId { get; set; }
    public bool Parking { get; set; }
    public string? IdempotencyKey { get; set; }

    protected Booking() { }

    public Booking(Guid id, Guid spaceId, Guid ownerUserId, DateTime startUtc, DateTime endUtc) : base(id)
    {
        SpaceId = spaceId;
        OwnerUserId = ownerUserId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = BookingStatus.Confirmed;
    }

    public string GetLifecycle(DateTime nowUtc)
    {
        if (Status == BookingStatus.Cancelled) return "cancelled";
        if (EndUtc <= nowUtc) return "ended";
        if (StartUtc <= nowUtc) return "in_progress";
        return "scheduled";
    }
}

public class MaintenanceWindow : FullAuditedAggregateRoot<Guid>
{
    public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Note { get; set; }
    public Guid? SeriesId { get; set; }
    /* Provenance only: what the admin originally targeted. SpaceId is authoritative. */
    public MaintenanceScopeType ScopeType { get; set; }
    public Guid ScopeId { get; set; }
    public MaintenanceStatus Status { get; set; }

    protected MaintenanceWindow() { }

    public MaintenanceWindow(Guid id, Guid spaceId, DateTime startUtc, DateTime endUtc) : base(id)
    {
        SpaceId = spaceId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = MaintenanceStatus.Active;
    }

    public string GetLifecycle(DateTime nowUtc)
    {
        if (Status == MaintenanceStatus.Cancelled) return "cancelled";
        if (EndUtc <= nowUtc) return "ended";
        if (StartUtc <= nowUtc) return "in_progress";
        return "scheduled";
    }
}

/* Business activity trail shown in the UI; separate from ABP's request AuditLog. */
public class ActivityLogEntry : AggregateRoot<Guid>
{
    public DateTime TimestampUtc { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Detail { get; set; } = null!;

    protected ActivityLogEntry() { }

    public ActivityLogEntry(Guid id) : base(id) { }
}
