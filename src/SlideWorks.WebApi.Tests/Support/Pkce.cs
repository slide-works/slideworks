using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Tests.Support;

// PKCE (RFC 7636) for tests: a random verifier and the S256 challenge for it. Uses only the BCL, because the acceptance
// project compiles this folder in too.
public static class Pkce
{
    public static string NewVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string S256(string verifier) => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}
