using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Profiles;

/* Any signed-in person may set, see and remove their own picture, and nobody else's: the picture is named by the
 * caller's id. It is kept with ABP's BLOB storing; the user only carries a version that changes with each upload. */
[Authorize]
public class ProfilePictureAppService : PortalAppService, IProfilePictureAppService
{
    /* The request field the SPA shows a refusal under. */
    public const string PictureField = "picture";

    private const int ReadChunkBytes = 81920;

    private readonly IBlobContainer<ProfilePictureContainer> _pictures;
    private readonly IdentityUserManager _users;

    public ProfilePictureAppService(IBlobContainer<ProfilePictureContainer> pictures, IdentityUserManager users)
    {
        _pictures = pictures;
        _users = users;
    }

    public async Task<IRemoteStreamContent?> GetAsync()
    {
        var bytes = await _pictures.GetAllBytesOrNullAsync(BlobName);
        if (bytes == null) return null;

        /* Only checked images are stored, so the format is always known. */
        var format = PictureFormat.Detect(bytes) ?? PictureFormat.Jpeg;
        return new RemoteStreamContent(new MemoryStream(bytes), $"profile-picture.{format.Extension}", format.ContentType);
    }

    public async Task<ProfilePictureDto> SetAsync(IRemoteStreamContent picture)
    {
        var bytes = await ReadWithinLimitAsync(picture);
        if (PictureFormat.Detect(bytes) == null) throw NotAnImage();

        /* The version is changed first: if storing the file fails, the whole request is rolled back. */
        var version = GuidGenerator.Create().ToString("N");
        await SetVersionAsync(version);
        await _pictures.SaveAsync(BlobName, bytes, overrideExisting: true);
        return new ProfilePictureDto { Version = version };
    }

    public async Task DeleteAsync()
    {
        await SetVersionAsync(null);
        await _pictures.DeleteAsync(BlobName);
    }

    private string BlobName => CurrentUser.GetId().ToString("N");

    /* Reads at most one byte past the limit, so an oversized upload is refused without being held in memory. */
    private async Task<byte[]> ReadWithinLimitAsync(IRemoteStreamContent? picture)
    {
        if (picture == null) throw NotAnImage();
        if (picture.ContentLength > ProfilePictureConsts.MaxBytes) throw TooLarge();

        await using var source = picture.GetStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await source.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > ProfilePictureConsts.MaxBytes) throw TooLarge();
        }
        return buffer.ToArray();
    }

    /* Null removes it. Left alone when it is already as asked, so removing twice changes nothing. */
    private async Task SetVersionAsync(string? version)
    {
        var user = await _users.GetByIdAsync(CurrentUser.GetId());
        if (user.GetProperty<string?>(ProfilePictureConsts.VersionPropertyName) == version) return;

        if (version == null) user.RemoveProperty(ProfilePictureConsts.VersionPropertyName);
        else user.SetProperty(ProfilePictureConsts.VersionPropertyName, version);
        (await _users.UpdateAsync(user)).CheckErrors();
    }

    private UserFriendlyException TooLarge()
        => new UserFriendlyException(code: PortalDomainErrorCodes.PictureTooLarge, message: L["Error:PictureTooLarge", ProfilePictureConsts.MaxMegabytes])
            .ForField(PictureField);

    private UserFriendlyException NotAnImage()
        => new UserFriendlyException(code: PortalDomainErrorCodes.PictureNotImage, message: L["Error:PictureNotImage"])
            .ForField(PictureField);
}
