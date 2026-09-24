using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Estate;

public interface IProfileLookupAppService : IApplicationService
{
    Task<CurrentUserProfileDto> GetCurrentAsync();
    Task<ListResultDto<UserLookupDto>> GetUsersAsync();
}
