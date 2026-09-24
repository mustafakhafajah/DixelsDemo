using System.Text.Json.Serialization;

namespace Dixels.Portal.Estate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpaceType
{
    MeetingRoom = 0,
    Equipment = 1,
    Desk = 2,
    Studio = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStatus
{
    Active = 0,
    Cancelled = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceScopeType
{
    Space = 0,
    Floor = 1,
    Building = 2
}
