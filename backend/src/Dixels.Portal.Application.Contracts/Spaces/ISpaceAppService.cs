using System;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Spaces;

/* GetListAsync (from ICrudAppService) is the one space collection: registry, "Find a space" and pickers. */
public interface ISpaceAppService : ICrudAppService<SpaceDto, Guid, GetSpaceListInput, CreateUpdateSpaceDto>
{
    Task<SpaceDto> SetBookableAsync(Guid id, SetBookableDto input);
}
