using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SlideWorks.Core.Join;

// The join code (J1): a 6-digit string from 100000 to 999999, standing for a deck.
public static class JoinCodes
{
    public const int MIN = 100000;
    public const int MAX = 999999;

    private const int RANGE = MAX - MIN + 1;

    // The largest multiple of RANGE that fits a uint; draws at or above it are redrawn so every code is equally likely.
    private const uint LIMIT = uint.MaxValue - (uint.MaxValue % RANGE) - 1;

    public static string New(RandomNumberGenerator random)
    {
        Span<byte> bytes = stackalloc byte[4];
        uint draw;
        do
        {
            random.GetBytes(bytes);
            draw = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
        while (draw > LIMIT);
        return (MIN + (draw % RANGE)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // "482913" becomes "482 913", the way it is read out loud and printed on a slide.
    public static string Display(string code) => $"{code[..3]} {code[3..]}";
}
