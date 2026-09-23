namespace Dixels.Portal.Estate;

public static class EstateConsts
{
    public const int MaxRecurrenceOccurrences = 200;

    public const int DefaultOpenHour = 8;
    public const int DefaultCloseHour = 20;
    public const int DefaultMinBookingMinutes = 15;
    public const int DefaultMaxBookingHours = 8;

    public const int MaxNameLength = 128;
    public const int MaxFloorNameLength = 32;
    public const int MaxTeamNameLength = 64;
    public const int MaxTimeZoneLength = 64;
    public const int MaxNoteLength = 500;
    public const int MaxIdempotencyKeyLength = 128;
    public const int MaxDetailLength = 1000;
}

/* Values match the mock's audit action strings so the UI can render them verbatim. */
public static class ActivityActions
{
    public const string BookingCreated = "booking.created";
    public const string BookingRescheduled = "booking.rescheduled";
    public const string BookingCancelled = "booking.cancelled";
    public const string BookingEndedEarly = "booking.ended_early";
    public const string SpaceCreated = "space.created";
    public const string SpaceUpdated = "space.updated";
    public const string SpaceStatusChanged = "space.status_changed";
    public const string BuildingCreated = "building.created";
    public const string BuildingUpdated = "building.updated";
    public const string BuildingStatusChanged = "building.status_changed";
    public const string FloorCreated = "floor.created";
    public const string FloorUpdated = "floor.updated";
    public const string FloorStatusChanged = "floor.status_changed";
    public const string SessionSignedIn = "session.signed_in";
    public const string MaintenanceScheduled = "maintenance.scheduled";
    public const string MaintenanceCancelled = "maintenance.cancelled";
}

public static class ActivityEntityTypes
{
    public const string Booking = "Booking";
    public const string Space = "Space";
    public const string Building = "Building";
    public const string Floor = "Floor";
    public const string Maintenance = "Maintenance";
    public const string Session = "Session";
}
