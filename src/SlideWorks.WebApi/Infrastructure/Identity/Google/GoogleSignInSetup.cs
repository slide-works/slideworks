using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// Sign-in with Google: the OpenID Connect code flow with state, nonce and PKCE, ending in the ordinary session cookie
// (GoogleSignInEvents). The scheme is always registered, because the client id is only known once configuration is
// read, and GoogleSignInStartup removes it when sign-in isn't set up.
public static class GoogleSignInSetup
{
    public const string SCHEME = "Google";

    public const string AUTHORITY = "https://accounts.google.com";

    // Google sends the browser back here. Register {SLIDEWORKS_PUBLIC_URL}/signin-google with Google, such as
    // http://localhost:3000/signin-google in development.
    public const string CALLBACK_PATH = "/signin-google";

    // Discovery, signing keys and the code exchange all go through this named client.
    public const string HTTP_CLIENT_NAME = "google-sign-in";

    public static IServiceCollection AddGoogleSignIn(this IServiceCollection services)
    {
        services.AddHttpClient(HTTP_CLIENT_NAME, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddAuthentication().AddOpenIdConnect(SCHEME, _ => { });
        services.AddOptions<OpenIdConnectOptions>(SCHEME).Configure<GoogleSignInSettings, IHttpClientFactory>((options, settings, clients) =>
        {
            options.Authority = AUTHORITY;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.CallbackPath = CALLBACK_PATH;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            // Query, not the handler's default form post: Google's redirect back is then a top-level GET, so the
            // correlation and nonce cookies can be SameSite=Lax, and no write ever lands outside /api. The redirect_uri
            // is built from the request, which PublicOriginMiddleware has already set to SLIDEWORKS_PUBLIC_URL's scheme
            // and host, and the cookies are Secure whenever that scheme is https.
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            // The defaults are openid and profile. Without email, Google's ID token carries no email claims.
            options.Scope.Add("email");
            options.Prompt = "select_account";
            options.MapInboundClaims = false;
            // Google documents both spellings of its issuer. The handler adds the discovery document's to these.
            options.TokenValidationParameters.ValidIssuers = ["accounts.google.com", AUTHORITY];
            options.Backchannel = clients.CreateClient(HTTP_CLIENT_NAME);
            options.EventsType = typeof(GoogleSignInEvents);
        });
        return services;
    }
}
