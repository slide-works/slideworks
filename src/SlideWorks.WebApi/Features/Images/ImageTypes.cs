namespace SlideWorks.WebApi.Features.Images;

// The image types SlideWorks accepts (K11, D14): PNG, JPEG, WebP and GIF, each recognised by its magic bytes. SVG is
// refused, because an SVG can carry script.
public static class ImageTypes
{
    public const int HEADER_LENGTH = 12;

    public const string PNG = "image/png";
    public const string JPEG = "image/jpeg";
    public const string WEBP = "image/webp";
    public const string GIF = "image/gif";

    public const string ACCEPTED_LIST = "image/png, image/jpeg, image/webp or image/gif";

    private static readonly byte[] PNG_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JPEG_SIGNATURE = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] GIF87_SIGNATURE = "GIF87a"u8.ToArray();
    private static readonly byte[] GIF89_SIGNATURE = "GIF89a"u8.ToArray();
    private static readonly byte[] RIFF = "RIFF"u8.ToArray();
    private static readonly byte[] WEBP_TAG = "WEBP"u8.ToArray();

    // The accepted type a declared content type names, in lower case, or null when it isn't one of the four.
    public static string? Normalise(string contentType)
    {
        var type = contentType.Trim().ToLowerInvariant();
        return type is PNG or JPEG or WEBP or GIF ? type : null;
    }

    // The short name of an accepted type, for messages: "PNG".
    public static string Label(string contentType) => contentType switch
    {
        PNG => "PNG",
        JPEG => "JPEG",
        WEBP => "WebP",
        _ => "GIF",
    };

    // True when the file's first bytes are those of contentType.
    public static bool Matches(string contentType, ReadOnlySpan<byte> header) => contentType switch
    {
        PNG => header.StartsWith(PNG_SIGNATURE),
        JPEG => header.StartsWith(JPEG_SIGNATURE),
        GIF => header.StartsWith(GIF87_SIGNATURE) || header.StartsWith(GIF89_SIGNATURE),
        WEBP => header.Length >= HEADER_LENGTH && header.StartsWith(RIFF) && header[8..12].SequenceEqual(WEBP_TAG),
        _ => false,
    };
}
