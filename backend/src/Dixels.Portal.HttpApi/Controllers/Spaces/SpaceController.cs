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
[Route("api/app/space")]
public class SpaceController : PortalController, ISpaceAppService
{
    private readonly ISpaceAppService _spaces;

    public SpaceController(ISpaceAppService spaces)
    {
        _spaces = spaces;
    }

    /* Spaces for pickers; BuildingId / FloorId narrow it to one building or floor. */
    [HttpGet]
    public Task<PagedResultDto<SpaceDto>> GetListAsync([FromQuery] GetSpaceListInput input) => _spaces.GetListAsync(input);

    /* "Find a space": bookable spaces matching the filters. */
    [HttpGet("bookable-list")]
    public Task<ListResultDto<SpaceDto>> GetBookableListAsync([FromQuery] FindSpacesInput input) => _spaces.GetBookableListAsync(input);

    /* One page of the admin registry. */
    [HttpGet("paged-list")]
    public Task<SpaceRegistryPageDto> GetPagedListAsync([FromQuery] GetSpacesInput input) => _spaces.GetPagedListAsync(input);

    [HttpGet("{id}")]
    public Task<SpaceDto> GetAsync(Guid id) => _spaces.GetAsync(id);

    [HttpPost]
    public Task<SpaceDto> CreateAsync([FromBody] CreateUpdateSpaceDto input) => _spaces.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<SpaceDto> UpdateAsync(Guid id, [FromBody] CreateUpdateSpaceDto input) => _spaces.UpdateAsync(id, input);

    [HttpPost("{id}/set-bookable")]
    public Task<SpaceDto> SetBookableAsync(Guid id, [FromBody] SetBookableDto input) => _spaces.SetBookableAsync(id, input);

    /* Deleting a space isn't offered: untick "Bookable" instead, so its booking history stays. */
    [NonAction]
    public Task DeleteAsync(Guid id) => _spaces.DeleteAsync(id);
}
