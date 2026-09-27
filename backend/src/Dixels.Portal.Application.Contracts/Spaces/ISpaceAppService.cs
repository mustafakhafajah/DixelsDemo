using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Spaces;

public interface ISpaceAppService : IApplicationService
{
    /* Every space; used by pickers (booking modal, find page, schedule). */
    Task<ListResultDto<SpaceDto>> GetListAsync();
    /* One filtered page for the admin registry, which must scale to thousands of spaces. */
    Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input);
    Task<SpaceDto> GetAsync(Guid id);
    Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input);
    Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input);
    Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input);
}
