using System;

namespace Dixels.Portal.Estate;

/* A start/end pair; used for booking series occurrences and maintenance occurrences. */
public class TimeWindowDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}
