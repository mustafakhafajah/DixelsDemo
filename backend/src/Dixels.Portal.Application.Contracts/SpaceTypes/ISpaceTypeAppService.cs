using System;
using Dixels.Portal.Estate;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.SpaceTypes;

/* DeleteAsync only succeeds for a type no space uses. */
public interface ISpaceTypeAppService : ICrudAppService<SpaceTypeDto, Guid, EstateListInput, CreateUpdateSpaceTypeDto>
{
}
