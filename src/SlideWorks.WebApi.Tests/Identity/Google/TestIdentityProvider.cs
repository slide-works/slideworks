using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// Stands in for Google at the HTTP boundary: the handler behind the app's "google-sign-in" named client, which carries
// discovery, the signing keys and the code exchange. Only the socket is fake; the app's OpenID Connect handler checks
// state, PKCE, nonce, signature, issuer, audience and expiry exactly as it would against Google. One RSA key signs every
// ID token Mint makes, and /token answers with NextIdToken.
public sealed class TestIdentityProvider : HttpMessageHandler
{
    public const string ISSUER = "https://accounts.google.com";

    public const string AUTHORIZATION_ENDPOINT = "https://accounts.google.com/o/oauth2/v2/auth";

    private const string KEY_ID = "test-key-1";
    private const string DISCOVERY = "https://accounts.google.com/.well-known/openid-configuration";
    private const string JWKS = "https://www.googleapis.com/oauth2/v3/certs";
    private const string TOKEN_ENDPOINT = "https://oauth2.googleapis.com/token";

    private readonly RSA _key = RSA.Create(2048);

    // What /token answers with on the next code exchange.
    public string? NextIdToken { get; set; }

    // The form of the last POST to /token.
    public IReadOnlyDictionary<string, string>? LastTokenRequest { get; private set; }

    // A signed RS256 ID token as Google issues it, for clientId, valid for an hour from now.
    public string Mint(string clientId, DateTimeOffset now, string subject, string email, string? name, bool emailVerified, string nonce, string? picture = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["email"] = email,
            ["email_verified"] = emailVerified,
            ["nonce"] = nonce,
        };
        if (name != null)
        {
            claims["name"] = name;
        }
        if (picture != null)
        {
            claims["picture"] = picture;
        }
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ISSUER,
            Audience = clientId,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key) { KeyId = KEY_ID }, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.GetLeftPart(UriPartial.Path);
        if (request.Method == HttpMethod.Get && url == DISCOVERY)
        {
            return Json(new Dictionary<string, object>
            {
                ["issuer"] = ISSUER,
                ["authorization_endpoint"] = AUTHORIZATION_ENDPOINT,
                ["token_endpoint"] = TOKEN_ENDPOINT,
                ["jwks_uri"] = JWKS,
                ["response_types_supported"] = new[] { "code" },
                ["subject_types_supported"] = new[] { "public" },
                ["id_token_signing_alg_values_supported"] = new[] { SecurityAlgorithms.RsaSha256 },
            });
        }
        if (request.Method == HttpMethod.Get && url == JWKS)
        {
            var parameters = _key.ExportParameters(includePrivateParameters: false);
            return Json(new
            {
                keys = new[]
                {
                    new { kty = "RSA", use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = KEY_ID, n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) },
                },
            });
        }
        if (request.Method == HttpMethod.Post && url == TOKEN_ENDPOINT)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastTokenRequest = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(form).ToDictionary(field => field.Key, field => field.Value.ToString());
            return Json(new Dictionary<string, object>
            {
                ["id_token"] = NextIdToken ?? "",
                ["access_token"] = "unused",
                ["token_type"] = "Bearer",
                ["expires_in"] = 3600,
            });
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _key.Dispose();
        }
        base.Dispose(disposing);
    }

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
