using System.Text;
using Dixels.Portal.Profiles;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Profiles;

/* A picture's format comes from its first bytes, never from its name or the type the browser claims. */
public class PictureFormatTests
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] WebP = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    [Fact]
    public void Jpeg_png_and_webp_are_recognised()
    {
        PictureFormat.Detect(Jpeg).ShouldBe(PictureFormat.Jpeg);
        PictureFormat.Detect(Png).ShouldBe(PictureFormat.Png);
        PictureFormat.Detect(WebP).ShouldBe(PictureFormat.WebP);
        PictureFormat.Detect(WebP)!.ContentType.ShouldBe("image/webp");
    }

    [Fact]
    public void Anything_else_is_not_a_picture()
    {
        PictureFormat.Detect([]).ShouldBeNull();
        PictureFormat.Detect(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")).ShouldBeNull();
        PictureFormat.Detect(Encoding.UTF8.GetBytes("MZ this is a program")).ShouldBeNull();
        /* A RIFF file that is not WebP (e.g. a WAV sound), and a WebP cut short. */
        PictureFormat.Detect([.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8]).ShouldBeNull();
        PictureFormat.Detect([.. "RIFF"u8, 0x24, 0x00]).ShouldBeNull();
        PictureFormat.Detect([0xFF, 0xD8]).ShouldBeNull();
    }
}
