using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Spaces;

/* One page of the registry, plus how many upcoming bookings each space on this page has. */
public class SpaceRegistryPageDto : PagedResultDto<SpaceDto>
{
    public Dictionary<Guid, int> UpcomingBookingCounts { get; set; } = new();
}
