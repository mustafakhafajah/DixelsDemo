using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Spaces;

/* Filters and paging for the admin space registry. Every filter is optional. */
public class GetSpacesInput : PagedResultRequestDto
{
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    /* Case-insensitive "contains". */
    public string? Name { get; set; }
    public Guid? TypeId { get; set; }
}
