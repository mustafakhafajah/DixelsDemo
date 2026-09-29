using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Users;

/* Read only: accounts, roles and permissions are changed in ABP's own Identity pages. */
public interface IUserDirectoryAppService : IApplicationService
{
    Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(UserDirectoryListInput input);
    Task<ListResultDto<UserDirectoryRoleDto>> GetRolesAsync();
}
