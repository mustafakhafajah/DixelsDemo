using System;

namespace Dixels.Portal.Maintenance;

public class OccurrenceAffectedCountDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int AffectedCount { get; set; }
}
