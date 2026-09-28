using System;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Floors;

public class GetFloorListInput : EstateListInput
{
    /* Only this building's floors; empty means every building. */
    public Guid? BuildingId { get; set; }
}
