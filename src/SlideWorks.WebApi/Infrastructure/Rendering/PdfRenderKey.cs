using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The PDF cache's key (P5, D13): "<deckId>:<revision>:<sessionId or none>:<viewHash>", where viewHash is a SHA-256 prefix
// of the whole print view as JSON (the deck and its results). Anything the print page would draw differently changes the key.
public static class PdfRenderKey
{
    public const string NO_SESSION = "none";

    // 16 hex characters (64 bits), as RenderKey's look hash.
    private const int VIEW_HASH_LENGTH = 16;

    public static string For(string deckId, long revision, Guid? sessionId, string viewJson) =>
        string.Join(
            ':',
            deckId,
            revision.ToString(CultureInfo.InvariantCulture),
            sessionId?.ToString("N") ?? NO_SESSION,
            ViewHash(viewJson));

    private static string ViewHash(string viewJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(viewJson)))[..VIEW_HASH_LENGTH];
}
