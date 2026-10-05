using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dixels.Portal.Profiles;

/* The signed-in person's own profile picture. Only ever about the caller: it takes no user id. */
public interface IProfilePictureAppService : IApplicationService
{
    /* Null when the person has no picture. */
    Task<IRemoteStreamContent?> GetAsync();

    /* A JPEG, PNG or WebP image of at most ProfilePictureConsts.MaxBytes; replaces any picture already there. */
    Task<ProfilePictureDto> SetAsync(IRemoteStreamContent picture);

    /* Back to the initials avatar. Nothing happens when there is no picture. */
    Task DeleteAsync();
}
