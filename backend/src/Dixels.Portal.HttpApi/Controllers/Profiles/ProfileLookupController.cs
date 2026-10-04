using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Profiles;

[RemoteService(Name = "Default")]
[Area("app")]
public class ProfileLookupController : PortalController, IProfileLookupAppService
{
    private readonly IProfileLookupAppService _profiles;

    public ProfileLookupController(IProfileLookupAppService profiles)
    {
        _profiles = profiles;
    }

    /* The signed-in user: name, email and whether they are an admin. */
    [HttpGet("api/app/users/me")]
    public Task<CurrentUserProfileDto> GetCurrentAsync() => _profiles.GetCurrentAsync();

    /* Admin: every user as id + name, for pickers. The full, paged user list is GET /api/app/users. */
    [HttpGet("api/app/user-summaries")]
    public Task<ListResultDto<UserLookupDto>> GetUsersAsync() => _profiles.GetUsersAsync();
}
