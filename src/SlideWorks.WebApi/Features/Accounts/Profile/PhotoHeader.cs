using System.Buffers.Binary;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// The pixel size a PNG, JPEG or WebP file's header gives, read from the bytes alone. Null when the header does not give one
// (a truncated or malformed file), which the upload refuses as a damaged image.
public static class PhotoHeader
{
    private const int PNG_IHDR = 12;
    private const int PNG_SIZE_END = 24;
    private const int WEBP_CHUNK = 12;

    public static (int Width, int Height)? SizeOf(string contentType, ReadOnlySpan<byte> bytes) => contentType switch
    {
        "image/png" => Valid(PngSize(bytes)),
        "image/jpeg" => Valid(JpegSize(bytes)),
        "image/webp" => Valid(WebpSize(bytes)),
        _ => null,
    };

    private static (int, int)? Valid((int Width, int Height)? size) => size is { Width: > 0, Height: > 0 } ? size : null;

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

    // Walks the marker segments after SOI until a start-of-frame marker, which holds the height then the width.
    private static (int, int)? JpegSize(ReadOnlySpan<byte> bytes)
    {
        var i = 2;
        while (i + 4 <= bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                return null;
            }
            var marker = bytes[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }
            if (marker is 0x01 or (>= 0xD0 and <= 0xD9))
            {
                if (marker is 0xD9 or 0xDA)
                {
                    return null;
                }
                i += 2;
                continue;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i + 2, 2));
            if (length < 2)
            {
                return null;
            }
            var isFrame = marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);
            if (isFrame)
            {
                if (i + 9 > bytes.Length)
                {
                    return null;
                }
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(i + 7, 2));
                return (width, height);
            }
            i += 2 + length;
        }
        return null;
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
