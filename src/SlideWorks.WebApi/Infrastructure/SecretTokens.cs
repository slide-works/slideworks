using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Infrastructure;

// Opaque bearer secrets that the server hands out once and keeps only as a hash: the OAuth codes, requests, client
// secrets and tokens, and the image upload tokens. A lookup hashes what the caller presents and compares hashes.
public static class SecretTokens
{
    // 256 random bits, base64url without padding (43 characters).
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    // Lower-case hex SHA-256 of the secret. Only this is stored, never the secret.
    public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
