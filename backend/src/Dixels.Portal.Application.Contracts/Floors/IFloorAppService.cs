using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Floors;

public interface IFloorAppService : IApplicationService
{
    Task<ListResultDto<FloorDto>> GetListAsync(Guid? buildingId);
    Task<FloorDto> CreateAsync(CreateUpdateFloorDto input);
    Task<FloorDto> UpdateAsync(Guid id, CreateUpdateFloorDto input);
    Task<FloorDto> SetStatusAsync(Guid id, SetStatusDto input);
}
