using System.Buffers.Binary;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The PNG and WebP rules, found by signature alone. The store has already enforced the size limit. An accepted file
// answers its pixel size when its header gives it ({"width": px, "height": px}), and is stored exactly as uploaded.
public static class RasterRules
{
    private static readonly byte[] PNG_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const int PNG_IHDR = 12;
    private const int PNG_SIZE_END = 24;
    private const int WEBP_CHUNK = 12;

    // Null when the bytes are neither a PNG nor a WebP (they may still be an SVG).
    public static AssetCheckResult? Check(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(PNG_SIGNATURE))
        {
            return AssetCheckResult.Accepted(AssetContentTypes.PNG, MetaOf(PngSize(bytes)));
        }
        if (bytes.Length >= WEBP_CHUNK && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return AssetCheckResult.Accepted(AssetContentTypes.WEBP, MetaOf(WebpSize(bytes)));
        }
        return null;
    }

    private static string? MetaOf((int Width, int Height)? size)
        => size is { Width: > 0, Height: > 0 } s ? $"{{\"width\":{s.Width},\"height\":{s.Height}}}" : null;

    // The IHDR chunk comes first: its length (13), its name, then the width and the height as 32-bit big-endian numbers.
    private static (int, int)? PngSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PNG_SIZE_END || !bytes.Slice(PNG_IHDR, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return width > int.MaxValue || height > int.MaxValue ? null : ((int)width, (int)height);
    }

    // The first chunk says how the size is stored: VP8 (lossy), VP8L (lossless) or VP8X (extended, a canvas size).
    private static (int, int)? WebpSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < WEBP_CHUNK + 8)
        {
            return null;
        }
        var kind = bytes.Slice(WEBP_CHUNK, 4);
        var data = bytes[(WEBP_CHUNK + 8)..];
        if (kind.SequenceEqual("VP8 "u8) && data.Length >= 10 && data[3] == 0x9D && data[4] == 0x01 && data[5] == 0x2A)
        {
            return (BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[8..]) & 0x3FFF);
        }
        if (kind.SequenceEqual("VP8L"u8) && data.Length >= 5 && data[0] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        if (kind.SequenceEqual("VP8X"u8) && data.Length >= 10)
        {
            return (Read24(data[4..]) + 1, Read24(data[7..]) + 1);
        }
        return null;
    }

    private static int Read24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
}
