using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.SpaceTypes;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.SpaceTypes;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/space-type")]
public class SpaceTypeController : PortalController, ISpaceTypeAppService
{
    private readonly ISpaceTypeAppService _types;

    public SpaceTypeController(ISpaceTypeAppService types)
    {
        _types = types;
    }

    [HttpGet]
    public Task<PagedResultDto<SpaceTypeDto>> GetListAsync([FromQuery] EstateListInput input) => _types.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<SpaceTypeDto> GetAsync(Guid id) => _types.GetAsync(id);

    [HttpPost]
    public Task<SpaceTypeDto> CreateAsync([FromBody] CreateUpdateSpaceTypeDto input) => _types.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<SpaceTypeDto> UpdateAsync(Guid id, [FromBody] CreateUpdateSpaceTypeDto input) => _types.UpdateAsync(id, input);

    /* Only a type no space uses can be deleted. */
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _types.DeleteAsync(id);
}
