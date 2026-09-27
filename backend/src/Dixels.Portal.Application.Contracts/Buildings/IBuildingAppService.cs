using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Buildings;

public interface IBuildingAppService : ICrudAppService<BuildingDto, Guid, EstateListInput, CreateUpdateBuildingDto>
{
    Task<BuildingDto> SetBookableAsync(Guid id, SetBookableDto input);
}
