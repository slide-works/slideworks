using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Client;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// The whole connection an agent makes with only the /mcp URL, through public routes: register, authorize as the signed-in
// presenter, consent, swap the code for tokens, then MCP over the real access token; and again after a refresh.
[TestFixture]
public class AgentConnectsOverOAuthTests : ApiTestFixture
{
    [Test]
    public async Task Connect_WhenPresenterConsents_ShouldListMcpToolsWithTheIssuedToken()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        var endpoint = new TokenEndpoint(Factory, anonymous);
        var clientId = await OAuthFlow.RegisterAsync(anonymous, "Claude");
        var requestId = await OAuthFlow.StartAsync(Client, clientId);
        var consent = await Client.PostAsJsonAsync("/api/oauth/consent/" + Uri.EscapeDataString(requestId), new { approve = true });
        consent.StatusCode.ShouldBe(HttpStatusCode.OK);
        var redirect = (await consent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirect").GetString().ShouldNotBeNull();
        var callback = OAuthFlow.CallbackQuery(redirect);
        callback["state"].ShouldBe(OAuthFlow.STATE);

        var tokens = await TokenEndpoint.OkBodyAsync(await endpoint.RedeemAsync(clientId, callback["code"]));

        var toolNames = await ListToolsAsync(tokens.GetProperty("access_token").GetString()!);
        toolNames.ShouldContain("create_deck");
        toolNames.ShouldContain("list_decks");

        var rotated = await TokenEndpoint.OkBodyAsync(await endpoint.RefreshAsync(clientId, tokens.GetProperty("refresh_token").GetString()!));
        (await ListToolsAsync(rotated.GetProperty("access_token").GetString()!)).ShouldContain("create_deck");
    }

    private async Task<List<string>> ListToolsAsync(string accessToken)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(OAuthFlow.Resource),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" },
            },
            Factory.CreateClient(),
            ownsHttpClient: true);
        await using var mcp = await McpClient.CreateAsync(transport);
        return (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToList();
    }
}
