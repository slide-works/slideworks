using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The share card's cache key and ETag (L9): "<deckId>:<revision>:<lookHash>:<sharing>", where lookHash is a SHA-256
// prefix of the resolved LookView JSON. Anything that changes the card changes the key: a new revision, a new look or
// a change of sharing (which decides between the deck's own card and the generic one). The generic card's key is
// "generic".
public static class RenderKey
{
    public const string GENERIC = "generic";

    // 16 hex characters (64 bits): plenty to tell one deck's looks apart.
    private const int LOOK_HASH_LENGTH = 16;

    public static string For(string deckId, int revision, string lookJson, string sharing) =>
        string.Join(':', deckId, revision.ToString(CultureInfo.InvariantCulture), LookHash(lookJson), sharing);

    private static string LookHash(string lookJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lookJson)))[..LOOK_HASH_LENGTH];
}
