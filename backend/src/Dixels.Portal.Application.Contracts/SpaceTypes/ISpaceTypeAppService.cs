using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.SpaceTypes;

public interface ISpaceTypeAppService : IApplicationService
{
    Task<ListResultDto<SpaceTypeDto>> GetListAsync();
    Task<SpaceTypeDto> CreateAsync(CreateUpdateSpaceTypeDto input);
    Task<SpaceTypeDto> UpdateAsync(Guid id, CreateUpdateSpaceTypeDto input);
    /* Only a type no space uses can be deleted. */
    Task DeleteAsync(Guid id);
}
