using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Floors;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/floor")]
public class FloorController : PortalController, IFloorAppService
{
    private readonly IFloorAppService _floors;

    public FloorController(IFloorAppService floors)
    {
        _floors = floors;
    }

    [HttpGet]
    public Task<PagedResultDto<FloorDto>> GetListAsync([FromQuery] GetFloorListInput input) => _floors.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<FloorDto> GetAsync(Guid id) => _floors.GetAsync(id);

    [HttpPost]
    public Task<FloorDto> CreateAsync([FromBody] CreateUpdateFloorDto input) => _floors.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<FloorDto> UpdateAsync(Guid id, [FromBody] CreateUpdateFloorDto input) => _floors.UpdateAsync(id, input);

    [HttpPost("{id}/set-bookable")]
    public Task<FloorDto> SetBookableAsync(Guid id, [FromBody] SetBookableDto input) => _floors.SetBookableAsync(id, input);

    /* Deleting a floor isn't offered: it would leave spaces and bookings behind. */
    [NonAction]
    public Task DeleteAsync(Guid id) => _floors.DeleteAsync(id);
}
