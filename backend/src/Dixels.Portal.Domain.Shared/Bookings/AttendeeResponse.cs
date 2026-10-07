using System;

namespace Dixels.Portal.Bookings;

/* An invited person's answer, as in Teams and Outlook. Everyone starts at None (not answered yet). Stored as a
 * number; the API uses the lower-case strings below. */
public enum AttendeeResponse
{
    None = 0,
    Accepted = 1,
    Tentative = 2,
    Declined = 3,
}

public static class AttendeeResponseExtensions
{
    /* The strings the API and the SPA use. This switch is the only place that knows them. */
    public static string ToApiValue(this AttendeeResponse response) => response switch
    {
        AttendeeResponse.None => "none",
        AttendeeResponse.Accepted => "accepted",
        AttendeeResponse.Tentative => "tentative",
        AttendeeResponse.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(response), response, null),
    };

    /* An answer someone can give: accepted, tentative or declined (not "none"). Case and spaces are ignored. */
    public static bool TryParseAnswer(string? value, out AttendeeResponse response)
    {
        response = value?.Trim().ToLowerInvariant() switch
        {
            "accepted" => AttendeeResponse.Accepted,
            "tentative" => AttendeeResponse.Tentative,
            "declined" => AttendeeResponse.Declined,
            _ => AttendeeResponse.None,
        };
        return response != AttendeeResponse.None;
    }
}
