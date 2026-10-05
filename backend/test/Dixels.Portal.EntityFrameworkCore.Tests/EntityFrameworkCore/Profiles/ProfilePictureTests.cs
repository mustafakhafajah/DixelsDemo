using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Profiles;

/* Everyone can set, change and remove their own picture, and only their own. Pictures are kept with ABP's BLOB
 * storing (in memory in tests, on disk under App_Data on the server). */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class ProfilePictureTests : PortalEntityFrameworkCoreTestBase
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private readonly IProfilePictureAppService _pictures;
    private readonly IMyProfileAppService _myProfile;
    private readonly IdentityUserManager _users;
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guids;

    public ProfilePictureTests()
    {
        _pictures = GetRequiredService<IProfilePictureAppService>();
        _myProfile = GetRequiredService<IMyProfileAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<ITenantRepository>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guids = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task A_new_picture_can_be_read_back_and_the_profile_shows_its_version()
    {
        var user = await CreateUserAsync();
        using var _ = _principal.As(user);

        (await _myProfile.GetAsync()).PictureVersion.ShouldBeNull();
        var set = await _pictures.SetAsync(Upload(Png, "photo.png", "image/png"));

        var picture = await _pictures.GetAsync();
        picture.ShouldNotBeNull();
        picture.ContentType.ShouldBe("image/png");
        (await BytesOf(picture)).ShouldBe(Png);
        (await _myProfile.GetAsync()).PictureVersion.ShouldBe(set.Version);
    }

    [Fact]
    public async Task A_new_picture_replaces_the_old_one_and_gets_a_new_version()
    {
        var user = await CreateUserAsync();
        using var _ = _principal.As(user);

        var first = await _pictures.SetAsync(Upload(Png, "first.png", "image/png"));
        var second = await _pictures.SetAsync(Upload(Jpeg, "second.jpg", "image/jpeg"));

        second.Version.ShouldNotBe(first.Version);
        var picture = await _pictures.GetAsync();
        picture!.ContentType.ShouldBe("image/jpeg");
        (await BytesOf(picture)).ShouldBe(Jpeg);
        (await _myProfile.GetAsync()).PictureVersion.ShouldBe(second.Version);
    }

    [Fact]
    public async Task Removing_the_picture_brings_back_none_and_removing_again_is_harmless()
    {
        var user = await CreateUserAsync();
        using var _ = _principal.As(user);
        await _pictures.SetAsync(Upload(Png, "photo.png", "image/png"));

        await _pictures.DeleteAsync();
        await _pictures.DeleteAsync();

        (await _pictures.GetAsync()).ShouldBeNull();
        (await _myProfile.GetAsync()).PictureVersion.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_picture_over_the_limit_is_refused_whether_or_not_its_size_is_given(bool sizeGiven)
    {
        var user = await CreateUserAsync();
        using var _ = _principal.As(user);
        var tooBig = Png.Concat(new byte[ProfilePictureConsts.MaxBytes]).ToArray();

        var refusal = await ShouldBeRefusedAsync(() => _pictures.SetAsync(Upload(tooBig, "big.png", "image/png", sizeGiven)));

        refusal.Code.ShouldBe(PortalDomainErrorCodes.PictureTooLarge);
        refusal.Message.ShouldContain(ProfilePictureConsts.MaxMegabytes.ToString());
        (await _pictures.GetAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task A_file_that_is_not_a_picture_is_refused_whatever_it_is_called()
    {
        var user = await CreateUserAsync();
        using var _ = _principal.As(user);

        var refusal = await ShouldBeRefusedAsync(() =>
            _pictures.SetAsync(Upload(Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "photo.png", "image/png")));

        refusal.Code.ShouldBe(PortalDomainErrorCodes.PictureNotImage);
        (await _pictures.GetAsync()).ShouldBeNull();
        (await _myProfile.GetAsync()).PictureVersion.ShouldBeNull();
    }

    [Fact]
    public async Task Each_person_only_ever_gets_their_own_picture()
    {
        var withPicture = await CreateUserAsync();
        var without = await CreateUserAsync();

        using (_principal.As(withPicture)) await _pictures.SetAsync(Upload(Png, "photo.png", "image/png"));

        using (_principal.As(without))
        {
            (await _pictures.GetAsync()).ShouldBeNull();
            (await _myProfile.GetAsync()).PictureVersion.ShouldBeNull();
        }
    }

    [Fact]
    public async Task A_tenants_pictures_are_kept_apart_from_the_hosts()
    {
        var tenant = await WithUnitOfWorkAsync(async () => await _tenants.InsertAsync(await _tenantManager.CreateAsync("pic" + Guid.NewGuid().ToString("N")[..8])));

        IdentityUser user;
        using (_currentTenant.Change(tenant.Id, tenant.Name))
        {
            user = await CreateUserAsync();
            using var _ = _principal.As(user);
            await _pictures.SetAsync(Upload(Png, "photo.png", "image/png"));
            (await _pictures.GetAsync()).ShouldNotBeNull();
        }

        /* The same name on the host side finds nothing. */
        using (_currentTenant.Change(null))
        using (_principal.As(new IdentityUser(user.Id, user.UserName, user.Email)))
        {
            (await _pictures.GetAsync()).ShouldBeNull();
        }
    }

    private static IRemoteStreamContent Upload(byte[] bytes, string fileName, string contentType, bool sizeGiven = true)
        => new RemoteStreamContent(new MemoryStream(bytes), fileName, contentType, sizeGiven ? bytes.Length : null);

    private static async Task<byte[]> BytesOf(IRemoteStreamContent content)
    {
        await using var stream = content.GetStream();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    /* Refused with a message the SPA shows under the picture. */
    private static async Task<UserFriendlyException> ShouldBeRefusedAsync(Func<Task> call)
    {
        var refusal = await Should.ThrowAsync<UserFriendlyException>(call);
        refusal.Data[ErrorFieldExtensions.FieldKey].ShouldBe(ProfilePictureAppService.PictureField);
        return refusal;
    }

    private Task<IdentityUser> CreateUserAsync() => WithUnitOfWorkAsync(async () =>
    {
        var tag = "pic" + Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser(_guids.Create(), tag, $"{tag}@dixels.io", _currentTenant.Id);
        (await _users.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return user;
    });
}
