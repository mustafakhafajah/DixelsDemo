using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Spaces;

/* Filters and paging for the admin space registry. Every filter is optional. */
public class GetSpacesInput : PagedResultRequestDto
{
    /* The ID as shown in the UI ("SP-3F2A1B9C"), any prefix of it, or a full GUID. */
    public string? Code { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    /* Case-insensitive "contains". */
    public string? Name { get; set; }
    public SpaceType? Type { get; set; }
}
