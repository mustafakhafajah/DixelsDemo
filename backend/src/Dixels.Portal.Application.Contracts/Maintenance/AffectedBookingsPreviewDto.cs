using System.Collections.Generic;

namespace Dixels.Portal.Maintenance;

public class AffectedBookingsPreviewDto
{
    public int SpaceCount { get; set; }
    public int TotalAffected { get; set; }
    public List<OccurrenceAffectedCountDto> PerOccurrence { get; set; } = new();
}
