using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// Who gets into /mcp (K1): a valid agent access token for this server's /mcp, and nobody else. The test host's public URL
// is http://localhost.
[TestFixture]
public class McpTransportTests : ApiTestFixture
{
    private const string RESOURCE_METADATA = "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"";

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
        var cookie = await SessionCookies.ForAsync(Factory, TestUser.Sam);
        using (var http = Factory.CreateClient())
        using (var me = new HttpRequestMessage(HttpMethod.Get, "/api/me"))
        {
            me.Headers.Add("Cookie", cookie);
            (await http.SendAsync(me)).StatusCode.ShouldBe(HttpStatusCode.OK, "the session cookie should sign the browser in on /api");
        }

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

    // Two Authorization headers are ambiguous, so neither is trusted, even when both carry the valid token.
    [TestCase(false)]
    [TestCase(true)]
    public async Task Mcp_WhenTwoAuthorizationHeaders_ShouldReturn401(bool bothValid)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var second = bothValid ? agent.Tokens.AccessToken : "junk";

        using var response = await PostInitializeAsync(request =>
            request.Headers.TryAddWithoutValidation("Authorization", [$"Bearer {agent.Tokens.AccessToken}", $"Bearer {second}"]).ShouldBeTrue());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The token is read from the Authorization header only, never from the query string, where it would end up in logs.
    [Test]
    public async Task Mcp_WhenTokenOnlyInTheQueryString_ShouldReturn401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        using var response = await PostInitializeAsync(path: $"/mcp?access_token={Uri.EscapeDataString(agent.Tokens.AccessToken)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldBe(RESOURCE_METADATA);
    }

    private static Action<HttpRequestMessage> Bearer(string token)
        => request => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // A bare client: the fixture's Client is signed in as a person, which must not be in play here.
    private async Task<HttpResponseMessage> PostInitializeAsync(Action<HttpRequestMessage>? configure = null, string path = "/mcp")
    {
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
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
}
