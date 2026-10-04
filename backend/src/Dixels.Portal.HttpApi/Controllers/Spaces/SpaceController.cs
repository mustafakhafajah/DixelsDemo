using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Spaces;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/spaces")]
public class SpaceController : PortalController, ISpaceAppService
{
    private readonly ISpaceAppService _spaces;

    public SpaceController(ISpaceAppService spaces)
    {
        _spaces = spaces;
    }

    /* The one space collection. Registry: ?buildingId&floorId&name&typeIds&skipCount&maxResultCount.
     * "Find a space": ?bookable=true plus its filters, and freeFromUtc / freeToUtc for "Free only". */
    [HttpGet]
    public Task<PagedResultDto<SpaceDto>> GetListAsync([FromQuery] GetSpaceListInput input) => _spaces.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<SpaceDto> GetAsync(Guid id) => _spaces.GetAsync(id);

    [HttpPost]
    public Task<SpaceDto> CreateAsync([FromBody] CreateUpdateSpaceDto input) => _spaces.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<SpaceDto> UpdateAsync(Guid id, [FromBody] CreateUpdateSpaceDto input) => _spaces.UpdateAsync(id, input);

    /* Partial update; the only field that can change this way is isBookable. */
    [HttpPatch("{id}")]
    public Task<SpaceDto> SetBookableAsync(Guid id, [FromBody] SetBookableDto input) => _spaces.SetBookableAsync(id, input);

    /* Deleting a space isn't offered: untick "Bookable" instead, so its booking history stays. */
    [NonAction]
    public Task DeleteAsync(Guid id) => _spaces.DeleteAsync(id);
}
