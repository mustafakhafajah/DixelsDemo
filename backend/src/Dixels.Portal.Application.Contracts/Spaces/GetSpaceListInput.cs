using System;
using System.Collections.Generic;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Spaces;

/* GET /api/app/spaces: the one space collection. The registry, "Find a space" and the pickers all read it,
 * each with the filters it needs. Every filter is optional; leaving one out means "any". */
public class GetSpaceListInput : EstateListInput
{
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    /* Case-insensitive "contains", in any language the space has a name in. */
    public string? Name { get; set; }
    /* Any of these types; empty means every type. */
    public List<Guid> TypeIds { get; set; } = new();
    /* At least this many seats; 0 or empty means any. */
    public int? MinCapacity { get; set; }
    /* true = only spaces that can be booked now (the space, its floor and its building are all bookable);
     * false = only those that can't. */
    public bool? Bookable { get; set; }
    /* "Free only": when both are given, only spaces that could be booked for [FreeFromUtc, FreeToUtc) come
     * back - no confirmed booking or blocked time in it, and the window fits the space's rules. */
    public DateTime? FreeFromUtc { get; set; }
    public DateTime? FreeToUtc { get; set; }
}
