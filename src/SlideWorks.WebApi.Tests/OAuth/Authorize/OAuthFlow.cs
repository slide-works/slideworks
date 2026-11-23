using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Authorize;

// The agent's side of the browser flow, through the public routes only: register, then GET /oauth/authorize.
public static class OAuthFlow
{
    public const string REDIRECT = "http://127.0.0.1:33418/callback";
    public const string STATE = "xyz-state";

    // RFC 7636 appendix B's example pair.
    public const string VERIFIER = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    public static readonly string CHALLENGE = Pkce.S256(VERIFIER);

    public static readonly string Resource = $"{TestAppFactory.PUBLIC_URL}/mcp";

    public static async Task<string> RegisterAsync(HttpClient client, string name = "Claude")
    {
        var response = await client.PostAsJsonAsync("/oauth/register", new { client_name = name, redirect_uris = new[] { REDIRECT } });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return body.GetProperty("client_id").GetString().ShouldNotBeNull();
    }

    // /oauth/authorize with a valid query for the client; each override replaces a parameter, and a null value drops it.
    public static string AuthorizePath(string clientId, params (string Name, string? Value)[] overrides)
    {
        var query = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = REDIRECT,
            ["state"] = STATE,
            ["code_challenge"] = CHALLENGE,
            ["code_challenge_method"] = "S256",
            ["scope"] = "decks",
            ["resource"] = Resource,
        };
        foreach (var (name, value) in overrides)
        {
            query[name] = value;
        }
        return QueryHelpers.AddQueryString("/oauth/authorize", query.Where(p => p.Value != null));
    }

    // A signed-in, approved presenter's authorize: the request id on the consent redirect.
    public static async Task<string> StartAsync(HttpClient presenter, string clientId)
    {
        var response = await presenter.GetAsync(AuthorizePath(clientId));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull().OriginalString;
        location.ShouldStartWith("/oauth/consent?request=", Case.Sensitive);
        return QueryHelpers.ParseQuery(new Uri("http://x" + location).Query)["request"].ToString();
    }

    // The query of a redirect back to the client, after checking it goes to the registered redirect URI.
    public static IReadOnlyDictionary<string, string> CallbackQuery(string redirect)
    {
        redirect.ShouldStartWith(REDIRECT + "?", Case.Sensitive);
        return QueryHelpers.ParseQuery(new Uri(redirect).Query).ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
    }
}
