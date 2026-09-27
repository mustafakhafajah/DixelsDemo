using System.Text.Json.Serialization;

namespace Dixels.Portal.Maintenance;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceScopeType
{
    Space = 0,
    Floor = 1,
    Building = 2
}
