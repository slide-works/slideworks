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

    // RFC 7636 S256: base64url(SHA-256(ASCII(verifier))) equals the challenge. Compared in fixed time.
    public static bool VerifierMatches(string codeVerifier, string codeChallenge)
    {
        var computed = Encoding.ASCII.GetBytes(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier))));
        return CryptographicOperations.FixedTimeEquals(computed, Encoding.ASCII.GetBytes(codeChallenge));
    }
}
