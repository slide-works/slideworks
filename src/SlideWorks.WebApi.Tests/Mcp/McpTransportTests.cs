using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// Who gets into /mcp (K1): a valid agent access token for this server's /mcp, and nobody else. The test host's public URL
// is http://localhost.
[TestFixture]
public class McpTransportTests : ApiTestFixture
{
    private const string RESOURCE_METADATA = "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"";

    // A browser session for this fixture only, under its own name so it never clashes with the app's cookie scheme. It is
    // the default scheme here, so a request carrying it is signed in everywhere except where a policy names its schemes.
    private const string BROWSER_SCHEME = "McpTransportTestsBrowser";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddAuthentication(options => options.DefaultScheme = BROWSER_SCHEME).AddCookie(BROWSER_SCHEME);

    [Test]
    public async Task Mcp_WhenNoToken_ShouldReturn401WithResourceMetadata()
    {
        using var response = await PostInitializeAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldBe(RESOURCE_METADATA);
        response.Headers.Location.ShouldBeNull();
    }

    [Test]
    public async Task Mcp_WhenCookieOnly_ShouldReturn401()
    {
        var cookie = await BrowserCookieForAsync(TestUser.Sam);

        using var response = await PostInitializeAsync(request => request.Headers.Add("Cookie", cookie));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldBe(RESOURCE_METADATA);
    }

    [Test]
    public async Task Mcp_WhenAgentToken_ShouldListTools()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var tools = await agent.Client.ListToolsAsync();

        tools.ShouldNotBeNull();
    }

    [Test]
    public async Task Mcp_WhenRevokedToken_ShouldReturn401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAgentTokens>().RevokeAsync(agent.Tokens.AccessToken);
        }

        using var response = await PostInitializeAsync(Bearer(agent.Tokens.AccessToken));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldBe($"{RESOURCE_METADATA}, error=\"invalid_token\"");
    }

    [Test]
    public async Task Mcp_WhenValidToken_ShouldAccept()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        using var response = await PostInitializeAsync(Bearer(agent.Tokens.AccessToken));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Mcp_WhenUnknownToken_ShouldReturn401AsInvalidToken()
    {
        using var response = await PostInitializeAsync(Bearer("not-a-real-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldBe($"{RESOURCE_METADATA}, error=\"invalid_token\"");
    }

    // The static API token guards /api today; it is not an agent token.
    [Test]
    public async Task Mcp_WhenStaticApiToken_ShouldReturn401()
    {
        using var response = await PostInitializeAsync(Bearer(TEST_TOKEN));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenTokenIssuedForAnotherResource_ShouldReturn401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        AgentTokenPair elsewhere;
        using (var scope = Factory.Services.CreateScope())
        {
            elsewhere = await scope.ServiceProvider.GetRequiredService<IAgentTokens>()
                .IssueAsync(new CodeGrant(agent.OAuthClient.ClientId, agent.PresenterId, "decks", "https://elsewhere.example/mcp"));
        }

        using var response = await PostInitializeAsync(Bearer(elsewhere.AccessToken));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenTokenExpired_ShouldReturn401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        using var response = await PostInitializeAsync(Bearer(agent.Tokens.AccessToken));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Action<HttpRequestMessage> Bearer(string token)
        => request => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // A bare client: the fixture's Client sends the static API token, which must not be in play here.
    private async Task<HttpResponseMessage> PostInitializeAsync(Action<HttpRequestMessage>? configure = null)
    {
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        configure?.Invoke(request);
        return await http.SendAsync(request);
    }

    // A real cookie for this fixture's browser scheme, and proof that it signs the request in: without that proof, a 401 on
    // /mcp could just mean the cookie was never valid.
    private async Task<string> BrowserCookieForAsync(TestUser user)
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BROWSER_SCHEME);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(SlideWorksClaims.PRESENTER_ID, Guid.NewGuid().ToString()),
                new Claim(SlideWorksClaims.EMAIL, user.Email),
                new Claim(SlideWorksClaims.NAME, user.Name),
                new Claim(SlideWorksClaims.ACTOR_KIND, nameof(ActorKind.Person)),
            ],
            BROWSER_SCHEME));
        var cookie = $"{options.Cookie.Name}={options.TicketDataFormat.Protect(new AuthenticationTicket(principal, BROWSER_SCHEME))}";

        using var scope = Factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Cookie = cookie;
        (await context.AuthenticateAsync()).Succeeded.ShouldBeTrue("the browser cookie should sign the request in under the default scheme");
        return cookie;
    }
}
