namespace Abstrict.Api.Services.Implementations;

/// <summary>Xác định định dạng ảnh từ nội dung thực (magic bytes), không tin Content-Type hay đuôi file do client gửi.</summary>
public static class KycImage
{
    public const long MaximumBytes = 5 * 1024 * 1024;

    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("image/png", ".png");
        return null;
    }

    public static string ContentTypeFromKey(string objectKey) =>
        objectKey.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg";
}
