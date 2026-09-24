using System.Text.Json.Serialization;

namespace Dixels.Portal.Maintenance;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStatus
{
    Active = 0,
    Cancelled = 1
}
