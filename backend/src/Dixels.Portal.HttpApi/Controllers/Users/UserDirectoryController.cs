using System.Threading.Tasks;
using Dixels.Portal.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Users;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/user-directory")]
public class UserDirectoryController : PortalController, IUserDirectoryAppService
{
    private readonly IUserDirectoryAppService _directory;

    public UserDirectoryController(IUserDirectoryAppService directory)
    {
        _directory = directory;
    }

    /* One page of users; search, role, active and locked are all applied on the server. */
    [HttpGet]
    public Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync([FromQuery] GetUserDirectoryInput input) => _directory.GetListAsync(input);
}
