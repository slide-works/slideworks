using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// The secret handling every OAuth store shares: new opaque secrets, the hash they are stored by, and the PKCE S256 check.
public static class OAuthSecrets
{
    // 256 random bits, base64url without padding (43 characters).
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    // Lower-case hex SHA-256 of the secret. Only this is stored, never the secret.
    public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    // RFC 7636 S256: base64url(SHA-256(ASCII(verifier))) equals the challenge. Compared in fixed time. A verifier that is
    // not 43 to 128 unreserved characters (A-Z, a-z, 0-9, '-', '.', '_', '~') never matches, which also keeps a non-ASCII
    // character from hashing as '?'.
    public static bool VerifierMatches(string codeVerifier, string codeChallenge)
    {
        if (!IsVerifier(codeVerifier))
        {
            return false;
        }
        var computed = Encoding.ASCII.GetBytes(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier))));
        return CryptographicOperations.FixedTimeEquals(computed, Encoding.ASCII.GetBytes(codeChallenge));
    }

    private static bool IsVerifier(string codeVerifier)
        => codeVerifier.Length is >= 43 and <= 128 && codeVerifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');
}
