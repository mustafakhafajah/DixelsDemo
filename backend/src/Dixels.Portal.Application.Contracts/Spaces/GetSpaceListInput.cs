using System;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Spaces;

/* The space list for pickers: every space, or only those in one building / floor. */
public class GetSpaceListInput : EstateListInput
{
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
}
