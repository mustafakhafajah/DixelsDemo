using System;

namespace Dixels.Portal.Profiles;

/* An image format a profile picture may have, told from the file's first bytes rather than its name or the type the
 * browser claims, so a renamed text file or program is not stored as a picture. */
public sealed class PictureFormat
{
    public static readonly PictureFormat Jpeg = new("image/jpeg", "jpg");
    public static readonly PictureFormat Png = new("image/png", "png");
    public static readonly PictureFormat WebP = new("image/webp", "webp");

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    /* "RIFF", four bytes of size, then "WEBP". */
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPSignature = "WEBP"u8.ToArray();
    private const int WebPSignatureOffset = 8;

    public string ContentType { get; }
    public string Extension { get; }

    private PictureFormat(string contentType, string extension)
    {
        ContentType = contentType;
        Extension = extension;
    }

    /* Null when the bytes are not a JPEG, PNG or WebP image. */
    public static PictureFormat? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(JpegSignature)) return Jpeg;
        if (bytes.StartsWith(PngSignature)) return Png;
        if (bytes.StartsWith(RiffSignature) && bytes.Length >= WebPSignatureOffset + WebPSignature.Length
            && bytes.Slice(WebPSignatureOffset, WebPSignature.Length).SequenceEqual(WebPSignature)) return WebP;
        return null;
    }
}
