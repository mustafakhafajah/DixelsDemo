using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Users;

public interface IUserDirectoryAppService : IApplicationService
{
    Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(GetUserDirectoryInput input);
}
