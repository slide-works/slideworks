using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// The PKCE S256 check the OAuth code store uses. New secrets and the hash they are stored by are SecretTokens', which the
// image uploads share.
public static class OAuthSecrets
{
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
