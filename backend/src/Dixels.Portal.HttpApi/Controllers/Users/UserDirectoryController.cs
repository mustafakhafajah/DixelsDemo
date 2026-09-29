using System.Threading.Tasks;
using Dixels.Portal.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Users;

/* Admin: the user directory (view only). */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/user-directory")]
public class UserDirectoryController : PortalController, IUserDirectoryAppService
{
    private readonly IUserDirectoryAppService _users;

    public UserDirectoryController(IUserDirectoryAppService users)
    {
        _users = users;
    }

    [HttpGet]
    public Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync([FromQuery] UserDirectoryListInput input) => _users.GetListAsync(input);

    /* Every role, admin first. */
    [HttpGet("roles")]
    public Task<ListResultDto<UserDirectoryRoleDto>> GetRolesAsync() => _users.GetRolesAsync();
}
