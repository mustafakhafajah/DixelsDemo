using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.TimeZones;

public class TimeZonePreferenceDto
{
    public const int MaxTimeZoneLength = 64;

    /* An IANA name such as "Asia/Amman"; null when the user has never chosen one (emails then use each building's
     * own zone). */
    [StringLength(MaxTimeZoneLength)]
    public string? TimeZone { get; set; }
}
