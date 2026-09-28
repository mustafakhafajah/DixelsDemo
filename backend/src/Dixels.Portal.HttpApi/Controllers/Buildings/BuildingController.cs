using System;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Buildings;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/building")]
public class BuildingController : PortalController, IBuildingAppService
{
    private readonly IBuildingAppService _buildings;

    public BuildingController(IBuildingAppService buildings)
    {
        _buildings = buildings;
    }

    [HttpGet]
    public Task<PagedResultDto<BuildingDto>> GetListAsync([FromQuery] EstateListInput input) => _buildings.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<BuildingDto> GetAsync(Guid id) => _buildings.GetAsync(id);

    [HttpPost]
    public Task<BuildingDto> CreateAsync([FromBody] CreateUpdateBuildingDto input) => _buildings.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<BuildingDto> UpdateAsync(Guid id, [FromBody] CreateUpdateBuildingDto input) => _buildings.UpdateAsync(id, input);

    [HttpPost("{id}/set-bookable")]
    public Task<BuildingDto> SetBookableAsync(Guid id, [FromBody] SetBookableDto input) => _buildings.SetBookableAsync(id, input);

    /* Deleting a building isn't offered: it would leave floors, spaces and bookings behind. */
    [NonAction]
    public Task DeleteAsync(Guid id) => _buildings.DeleteAsync(id);
}
