using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.Core.Rendering;

// A render token is "<expiry>.<mac>": the expiry in Unix milliseconds, and an HMAC-SHA256 over the purpose,
// the expiry and the deck id, keyed by RenderTokenOptions.Key. The deck id isn't in the token, so Validate
// recomputes the MAC for the deck it is asked about: another deck, another purpose or a changed expiry
// gives a different MAC. Nothing is stored, so a token works for every request until it expires.
public sealed class RenderTokens : IRenderTokens
{
    private static readonly TimeSpan PREVIEW_LIFETIME = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CARD_LIFETIME = TimeSpan.FromSeconds(60);
    private const int MAC_BYTES = 32;

    private readonly byte[] _key;
    private readonly TimeProvider _time;

    public RenderTokens(RenderTokenOptions options, TimeProvider time)
    {
        if (options.Key.Length < RenderTokenOptions.MINIMUM_KEY_LENGTH)
        {
            throw new InvalidOperationException(
                $"{RenderTokenOptions.SECTION}:Key must be at least {RenderTokenOptions.MINIMUM_KEY_LENGTH} characters");
        }

        _key = Encoding.UTF8.GetBytes(options.Key);
        _time = time;
    }

    // How long a token of this purpose lives (L9): long enough for an agent to install a browser before it
    // previews, and only as long as one card render needs.
    public static TimeSpan LifetimeOf(RenderPurpose purpose) => purpose switch
    {
        RenderPurpose.Preview => PREVIEW_LIFETIME,
        RenderPurpose.Card => CARD_LIFETIME,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown render purpose"),
    };

    public string Issue(string deckId, RenderPurpose purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(deckId);
        var expiry = _time.GetUtcNow().Add(LifetimeOf(purpose)).ToUnixTimeMilliseconds();
        return expiry.ToString(CultureInfo.InvariantCulture) + "." + Base64Url.EncodeToString(Mac(deckId, purpose, expiry));
    }

    public bool Validate(string? token, string deckId, RenderPurpose purpose)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(deckId) || !Enum.IsDefined(purpose))
        {
            return false;
        }

        var dot = token.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0
            || !long.TryParse(token.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out var expiry)
            || !TryDecodeMac(token.AsSpan(dot + 1), out var mac))
        {
            return false;
        }

        // The MAC is checked before the clock, in constant time, so a forged token learns nothing from timing.
        return CryptographicOperations.FixedTimeEquals(mac, Mac(deckId, purpose, expiry))
            && _time.GetUtcNow().ToUnixTimeMilliseconds() < expiry;
    }

    private static bool TryDecodeMac(ReadOnlySpan<char> text, out byte[] mac)
    {
        mac = new byte[MAC_BYTES];
        return Base64Url.IsValid(text, out var length)
            && length == MAC_BYTES
            && Base64Url.TryDecodeFromChars(text, mac, out var written)
            && written == MAC_BYTES;
    }

    // The purpose and the expiry have fixed widths and the deck id comes last, so no two inputs share bytes.
    private byte[] Mac(string deckId, RenderPurpose purpose, long expiry)
    {
        var deck = Encoding.UTF8.GetBytes(deckId);
        var message = new byte[1 + sizeof(long) + deck.Length];
        message[0] = (byte)purpose;
        BinaryPrimitives.WriteInt64BigEndian(message.AsSpan(1), expiry);
        deck.CopyTo(message, 1 + sizeof(long));
        return HMACSHA256.HashData(_key, message);
    }
}
