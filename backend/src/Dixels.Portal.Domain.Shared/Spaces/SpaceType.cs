using System.Text.Json.Serialization;

namespace Dixels.Portal.Spaces;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpaceType
{
    MeetingRoom = 0,
    Equipment = 1,
    Desk = 2,
    Studio = 3
}
