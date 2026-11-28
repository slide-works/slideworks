using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// The device secrets and sign-in codes (K4): 32 random bytes, base64url without padding, a secret prefixed swd_ so a
// leaked one is findable by secret scanners. Both are stored only as SHA-256 hex. The PKCE check is S256, as OAuth's.
public static class DeviceSecrets
{
    public const string SECRET_PREFIX = "swd_";

    public static string NewSecret() => SECRET_PREFIX + SecretTokens.New();

    public static string NewCode() => SecretTokens.New();

    public static string Hash(string value) => SecretTokens.Hash(value);

    // challenge == base64url(SHA-256(ASCII(verifier))), compared in fixed time; a verifier that is not 43 to 128
    // unreserved characters never matches.
    public static bool ChallengeMatches(string verifier, string challenge) => OAuthSecrets.VerifierMatches(verifier, challenge);
}
