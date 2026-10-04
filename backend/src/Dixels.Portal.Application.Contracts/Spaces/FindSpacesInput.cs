using System;
using System.Collections.Generic;

namespace Dixels.Portal.Spaces;

/* The "Find a space" filters. Every filter is optional; only bookable spaces are ever returned. */
public class FindSpacesInput
{
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    /* At least this many seats; 0 or empty means any. */
    public int? MinCapacity { get; set; }
    /* Any of these types; empty means every type. */
    public List<Guid> TypeIds { get; set; } = new();
    /* Case-insensitive "contains". */
    public string? Name { get; set; }
    /* "Free only": when both are given, only spaces that could be booked for [FreeFromUtc, FreeToUtc) come
     * back - no confirmed booking or blocked time in it, and the window fits the space's rules. */
    public DateTime? FreeFromUtc { get; set; }
    public DateTime? FreeToUtc { get; set; }
}
