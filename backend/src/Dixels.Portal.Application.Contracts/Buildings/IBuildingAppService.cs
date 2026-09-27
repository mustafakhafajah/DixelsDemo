using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Buildings;

public interface IBuildingAppService : IApplicationService
{
    Task<ListResultDto<BuildingDto>> GetListAsync();
    Task<BuildingDto> GetAsync(Guid id);
    Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input);
    Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input);
    Task<BuildingDto> SetStatusAsync(Guid id, SetStatusDto input);
}
