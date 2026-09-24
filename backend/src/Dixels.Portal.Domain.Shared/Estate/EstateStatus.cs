using System.Text.Json.Serialization;

namespace Dixels.Portal.Estate;

/* Shared by buildings, floors and spaces, so it lives in Estate rather than one aggregate's folder. */
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstateStatus
{
    Active = 0,
    Inactive = 1
}
