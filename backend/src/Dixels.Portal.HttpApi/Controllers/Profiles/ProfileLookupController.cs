using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Profiles;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/profile-lookup")]
public class ProfileLookupController : PortalController, IProfileLookupAppService
{
    private readonly IProfileLookupAppService _profiles;

    public ProfileLookupController(IProfileLookupAppService profiles)
    {
        _profiles = profiles;
    }

    /* The signed-in user: name, email and whether they are an admin. */
    [HttpGet("current")]
    public Task<CurrentUserProfileDto> GetCurrentAsync() => _profiles.GetCurrentAsync();

    /* Admin: every user, for pickers. */
    [HttpGet("users")]
    public Task<ListResultDto<UserLookupDto>> GetUsersAsync() => _profiles.GetUsersAsync();
}
