using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Floors;

public interface IFloorAppService : ICrudAppService<FloorDto, Guid, GetFloorListInput, CreateUpdateFloorDto>
{
    Task<FloorDto> SetBookableAsync(Guid id, SetBookableDto input);
}
