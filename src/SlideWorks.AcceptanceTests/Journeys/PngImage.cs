using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SlideWorks.AcceptanceTests.Journeys;

// A real PNG for an agent to upload, made here so the test needs no image file: 8-bit RGB, a left-to-right blue
// gradient over a light ground, which a browser decodes and draws.
public static class PngImage
{
    private static readonly byte[] SIGNATURE = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CRC_TABLE = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c;
    }).ToArray();

    public static byte[] Gradient(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB

        var rows = new byte[height * (1 + (width * 3))];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + (width * 3)); // each row starts with filter type 0
            for (var x = 0; x < width; x++)
            {
                var shade = (byte)(230 - (x * 160 / Math.Max(1, width - 1)));
                rows[row + 1 + (x * 3)] = shade;
                rows[row + 2 + (x * 3)] = shade;
                rows[row + 3 + (x * 3)] = 245;
            }
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(rows);
        }

        using var png = new MemoryStream();
        png.Write(SIGNATURE);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeAndData));
        png.Write(number);
    }

    private static uint Crc(byte[] bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            c = CRC_TABLE[(c ^ b) & 0xFF] ^ (c >> 8);
        }
        return c ^ 0xFFFFFFFFu;
    }
}
