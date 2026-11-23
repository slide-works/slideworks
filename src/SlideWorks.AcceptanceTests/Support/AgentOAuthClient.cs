using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Support;

// An agent's OAuth client, doing what Claude Code or Cursor does when it is given only the /mcp URL: an unauthenticated
// call to /mcp gets a 401 whose WWW-Authenticate names the protected-resource metadata, that names the authorization
// server, whose metadata lists the endpoints. The client registers itself dynamically (RFC 7591), sends the presenter's
// browser to /oauth/authorize with a PKCE S256 challenge, the presenter allows it on the consent page, and the browser
// lands on the client's loopback callback with a code, which the client exchanges for tokens with its verifier.
// Nothing here reaches into the app's container: every step is HTTP or the browser.
public sealed partial class AgentOAuthClient : IDisposable
{
    // The agent's loopback listener. The browser never reaches it: AuthorizeAsync answers it with page.RouteAsync and
    // reads the code from the URL, as the listener would.
    public const string CALLBACK = "http://127.0.0.1:33418/callback";

    private const string SCOPE = "decks";

    private readonly HttpClient _http;
    private readonly AuthorizationServer _server;

    private AgentOAuthClient(HttpClient http, AuthorizationServer server, string resource, string clientId, string clientName)
    {
        _http = http;
        _server = server;
        Resource = resource;
        ClientId = clientId;
        ClientName = clientName;
    }

    // The MCP endpoint the tokens are for, as the protected-resource metadata names it.
    public string Resource { get; }

    public string ClientId { get; }

    public string ClientName { get; }

    // Discovers the authorization server from /mcp's 401 and registers a public client named clientName. Registration
    // is limited per IP (10 a minute), so register once and authorize as often as needed.
    public static async Task<AgentOAuthClient> RegisterAsync(string baseUrl, string clientName)
    {
        var http = new HttpClient();
        try
        {
            var metadataUrl = await ResourceMetadataUrlAsync(http, baseUrl + "/mcp");
            var resourceDoc = await GetJsonAsync(http, metadataUrl);
            var resource = resourceDoc.GetProperty("resource").GetString().ShouldNotBeNull();
            var issuer = resourceDoc.GetProperty("authorization_servers")[0].GetString().ShouldNotBeNull();
            var server = AuthorizationServer.From(await GetJsonAsync(http, issuer + "/.well-known/oauth-authorization-server"));

            using var registered = await http.PostAsJsonAsync(server.RegistrationEndpoint, new Dictionary<string, object>
            {
                ["client_name"] = clientName,
                ["redirect_uris"] = new[] { CALLBACK },
                ["grant_types"] = new[] { "authorization_code", "refresh_token" },
                ["response_types"] = new[] { "code" },
                ["token_endpoint_auth_method"] = "none",
            });
            registered.StatusCode.ShouldBe(HttpStatusCode.Created, await registered.Content.ReadAsStringAsync());
            var clientId = (await ReadJsonAsync(registered)).GetProperty("client_id").GetString().ShouldNotBeNull();
            return new AgentOAuthClient(http, server, resource, clientId, clientName);
        }
        catch
        {
            http.Dispose();
            throw;
        }
    }

    // Sends the browser, signed in as presenter, through /oauth/authorize and the consent page, allows the agent, and
    // exchanges the code the callback gets for an access token.
    public async Task<string> AuthorizeAsync(IPage browser, TestUser presenter)
    {
        var verifier = Pkce.NewVerifier();
        var challenge = Pkce.S256(verifier);
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));

        await SignIn.AsAsync(browser, presenter);
        await browser.RouteAsync(CALLBACK + "**", route => route.FulfillAsync(new() { ContentType = "text/plain", Body = "You can close this tab." }));
        await browser.GotoAsync(QueryHelpers.AddQueryString(_server.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = CALLBACK,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["scope"] = SCOPE,
            ["resource"] = Resource,
        }));
        var consent = new ConsentPage(browser);
        await consent.ShouldAskForAsync(ClientName);
        await consent.AllowAsync();
        await Expect(browser).ToHaveURLAsync(Callback());
        await browser.UnrouteAsync(CALLBACK + "**");

        var answer = QueryHelpers.ParseQuery(new Uri(browser.Url).Query);
        answer["state"].ToString().ShouldBe(state);
        return await ExchangeAsync(answer["code"].ToString(), verifier);
    }

    public void Dispose() => _http.Dispose();

    private async Task<string> ExchangeAsync(string code, string verifier)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = CALLBACK,
            ["code_verifier"] = verifier,
            ["resource"] = Resource,
        });
        using var response = await _http.PostAsync(_server.TokenEndpoint, form);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tokens = await ReadJsonAsync(response);
        tokens.GetProperty("token_type").GetString().ShouldBe("Bearer");
        tokens.GetProperty("scope").GetString().ShouldBe(SCOPE);
        tokens.GetProperty("refresh_token").GetString().ShouldNotBeNullOrEmpty();
        var accessToken = tokens.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrEmpty();
        return accessToken;
    }

    // An MCP initialize without a token: the 401's Bearer challenge carries resource_metadata (RFC 9728).
    private static async Task<string> ResourceMetadataUrlAsync(HttpClient http, string mcpUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, mcpUrl)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "agent", version = "1.0" } },
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await http.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var bearer = response.Headers.WwwAuthenticate.Single(h => h.Scheme == "Bearer");
        var match = ResourceMetadata().Match(bearer.Parameter ?? string.Empty);
        match.Success.ShouldBeTrue($"No resource_metadata in WWW-Authenticate: Bearer {bearer.Parameter}");
        return match.Groups["url"].Value;
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient http, string url)
    {
        using var response = await http.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, url);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("resource_metadata=\"(?<url>[^\"]+)\"")]
    private static partial Regex ResourceMetadata();

    [GeneratedRegex(@"^http://127\.0\.0\.1:33418/callback\?")]
    private static partial Regex Callback();

    // The endpoints from the authorization server's metadata (RFC 8414) this client uses.
    private sealed record AuthorizationServer(string AuthorizationEndpoint, string TokenEndpoint, string RegistrationEndpoint)
    {
        public static AuthorizationServer From(JsonElement metadata)
        {
            metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(m => m.GetString()).ShouldContain("S256");
            return new AuthorizationServer(
                metadata.GetProperty("authorization_endpoint").GetString().ShouldNotBeNull(),
                metadata.GetProperty("token_endpoint").GetString().ShouldNotBeNull(),
                metadata.GetProperty("registration_endpoint").GetString().ShouldNotBeNull());
        }
    }
}
