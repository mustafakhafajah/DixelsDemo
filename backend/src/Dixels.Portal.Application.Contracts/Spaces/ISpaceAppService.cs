using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Spaces;

/* GetListAsync (from ICrudAppService): every space, for pickers (booking modal, schedule). */
public interface ISpaceAppService : ICrudAppService<SpaceDto, Guid, EstateListInput, CreateUpdateSpaceDto>
{
    /* "Find a space": only bookable spaces, filtered in the database. */
    Task<ListResultDto<SpaceDto>> GetBookableListAsync(FindSpacesInput input);
    /* One filtered page for the admin registry, which must scale to thousands of spaces. */
    Task<SpaceRegistryPageDto> GetPagedListAsync(GetSpacesInput input);
    Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input);
}
