using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Spaces;

public interface ISpaceAppService : IApplicationService
{
    Task<ListResultDto<SpaceDto>> GetListAsync();
    Task<SpaceDto> GetAsync(Guid id);
    Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input);
    Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input);
    Task<SpaceDto> SetStatusAsync(Guid id, SetStatusDto input);
}
