using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Portal.Profiles;

/* The picture endpoints over real HTTP, as the SPA calls them: a multipart upload, the image back with its type,
 * and 204 when there is none. Tests run as the test principal's user (FakeCurrentPrincipalAccessor). */
public class ProfilePictureHttpTests : PortalWebTestBase
{
    private const string Url = "/api/app/my-profile/picture";
    /* FakeCurrentPrincipalAccessor's user id. */
    private static readonly Guid TestUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    [Fact]
    public async Task Upload_read_back_and_remove_over_http()
    {
        await EnsureTestUserAsync();

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var upload = await Client.PutAsync(Url, Picture(Png, "photo.png", "image/png"));
        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await upload.Content.ReadFromJsonAsync<ProfilePictureDto>())!.Version.ShouldNotBeNullOrWhiteSpace();

        var picture = await Client.GetAsync(Url);
        picture.StatusCode.ShouldBe(HttpStatusCode.OK);
        picture.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await picture.Content.ReadAsByteArrayAsync()).ShouldBe(Png);

        (await Client.DeleteAsync(Url)).IsSuccessStatusCode.ShouldBeTrue();
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_file_that_is_not_a_picture_is_refused_with_400_naming_the_field()
    {
        await EnsureTestUserAsync();

        var response = await Client.PutAsync(Url, Picture("not a picture"u8.ToArray(), "photo.png", "image/png"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain(PortalDomainErrorCodes.PictureNotImage);
        body.ShouldContain("\"field\":\"picture\"");
    }

    /* The form field the controller binds the picture from (its parameter name). */
    private static MultipartFormDataContent Picture(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "picture", fileName } };
    }

    private async Task EnsureTestUserAsync()
    {
        var users = GetRequiredService<IdentityUserManager>();
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        if (await users.FindByIdAsync(TestUserId.ToString()) == null)
            (await users.CreateAsync(new IdentityUser(TestUserId, "picture.tester", "picture.tester@example.com"), "1q2w3E*")).Succeeded.ShouldBeTrue();
        await uow.CompleteAsync();
    }
}
