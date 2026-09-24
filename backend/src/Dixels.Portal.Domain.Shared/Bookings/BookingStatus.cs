using System.Text.Json.Serialization;

namespace Dixels.Portal.Bookings;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1
}
