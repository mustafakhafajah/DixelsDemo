using System;
using System.Collections.Generic;

namespace Dixels.Portal.Spaces;

/* The "Find a space" filters. Every filter is optional; only bookable spaces are ever returned. */
public class FindSpacesInput
{
    public Guid? BuildingId { get; set; }
    /* Floors are matched by name ("2"), as the page lists floor names for the chosen building. */
    public string? FloorName { get; set; }
    /* At least this many seats; 0 or empty means any. */
    public int? MinCapacity { get; set; }
    /* Any of these types; empty means every type. */
    public List<Guid> TypeIds { get; set; } = new();
    /* Case-insensitive "contains". */
    public string? Name { get; set; }
}
