using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dixels.Portal.Controllers.Profiles;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/my-profile/picture")]
public class ProfilePictureController : PortalController, IProfilePictureAppService
{
    private readonly IProfilePictureAppService _pictures;

    public ProfilePictureController(IProfilePictureAppService pictures)
    {
        _pictures = pictures;
    }

    /* The image itself, or 204 when the person has no picture (ABP's stream formatter would answer an empty 200). */
    [HttpGet]
    public async Task<IRemoteStreamContent?> GetAsync()
    {
        var picture = await _pictures.GetAsync();
        if (picture == null) Response.StatusCode = StatusCodes.Status204NoContent;
        return picture;
    }

    /* multipart/form-data with the image in the "picture" field; replaces any picture already there. */
    [HttpPut]
    public Task<ProfilePictureDto> SetAsync(IRemoteStreamContent picture) => _pictures.SetAsync(picture);

    [HttpDelete]
    public Task DeleteAsync() => _pictures.DeleteAsync();
}
