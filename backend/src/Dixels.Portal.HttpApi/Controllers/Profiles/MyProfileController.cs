using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Profiles;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/my-profile")]
public class MyProfileController : PortalController, IMyProfileAppService
{
    private readonly IMyProfileAppService _myProfile;

    public MyProfileController(IMyProfileAppService myProfile)
    {
        _myProfile = myProfile;
    }

    /* The signed-in user's own record; 401 when signed out. */
    [HttpGet]
    public Task<MyProfileDto> GetAsync() => _myProfile.GetAsync();
}
