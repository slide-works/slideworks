using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// "Sign in with Google", from SLIDEWORKS_GOOGLE_CLIENT_ID and SLIDEWORKS_GOOGLE_CLIENT_SECRET, read when first resolved
// (after every configuration source, test overrides included). On only when both are set. Anything else leaves sign-in
// off and the server running: /signin says sign-in isn't set up, and /signin/google answers 503. A class, not a record,
// so a stray ToString can't print the secret.
[RegisterSingleton]
public sealed class GoogleSignInSettings
{
    public const string CLIENT_ID_KEY = "SLIDEWORKS_GOOGLE_CLIENT_ID";

    public const string CLIENT_SECRET_KEY = "SLIDEWORKS_GOOGLE_CLIENT_SECRET";

    public GoogleSignInSettings(IConfiguration configuration, ILogger<GoogleSignInSettings> logger)
    {
        var clientId = Read(configuration, CLIENT_ID_KEY);
        var clientSecret = Read(configuration, CLIENT_SECRET_KEY);
        if ((clientId == null) != (clientSecret == null))
        {
            logger.LogWarning(
                "Sign-in is off: {Set} is set but {Missing} is not. Set both to turn on sign-in with Google.",
                clientId == null ? CLIENT_SECRET_KEY : CLIENT_ID_KEY,
                clientId == null ? CLIENT_ID_KEY : CLIENT_SECRET_KEY);
            return;
        }
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    public string? ClientId { get; }

    public string? ClientSecret { get; }

    public bool IsConfigured => ClientId != null && ClientSecret != null;

    private static string? Read(IConfiguration configuration, string key)
        => configuration[key]?.Trim() is { Length: > 0 } value ? value : null;
}
