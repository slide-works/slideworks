using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// set_sharing with person and access (D20): one person added, changed or taken off through SharingService, so the share log
// sees it like the PUT. A partial email is refused and nothing is stored. Every test uses emails of its own.
[TestFixture]
public class SetSharingPersonTests : ApiTestFixture
{
    [Test]
    public async Task SetSharing_WhenPersonAndWrite_ShouldAddOne()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var alex = Fresh("alex");
        var mira = Fresh("mira");
        await agent.CallToolAsync("set_sharing", new { deck, level = "named", person = mira, access = "read" });

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, person = alex, access = "write" });

        isError.ShouldBeFalse(result.ToString());
        result.GetProperty("level").GetString().ShouldBe("named");
        result.GetProperty("people").EnumerateArray().Select(p => (p.GetProperty("email").GetString(), p.GetProperty("access").GetString()))
            .ShouldBe([(alex, "write"), (mira, "read")], ignoreOrder: true);
        result.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe([alex, mira], ignoreOrder: true);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe($"changed sharing: Named people (+{alex} write)");
    }

    [Test]
    public async Task SetSharing_WhenRemove_ShouldTakeOff()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var alex = Fresh("alex");
        var mira = Fresh("mira");
        await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = new[] { alex, mira } });

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, person = alex, access = "remove" });

        isError.ShouldBeFalse(result.ToString());
        result.GetProperty("people").EnumerateArray().Select(p => p.GetProperty("email").GetString()).ShouldBe([mira]);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe($"changed sharing: Named people (-{alex})");
    }

    [Test]
    public async Task SetSharing_WhenPartialEmail_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, person = "alex", access = "write" });
        var wire = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","person":"@team.example","access":"read"}""");

        isError.ShouldBeTrue();
        result.GetString().ShouldBe("person: \"alex\" is not an email address");
        wire.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(wire).ShouldBe("person: \"@team.example\" is not an email address");
        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        view.GetProperty("people").GetArrayLength().ShouldBe(0);
        view.GetProperty("level").GetString().ShouldBe("private");
    }

    [Test]
    public async Task SetSharing_WhenPersonAdded_ShouldLogThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var alex = Fresh("alex");
        var mira = Fresh("mira");
        await agent.CallToolAsync("set_sharing", new { deck, level = "named", person = mira, access = "read" });
        using var scope = Factory.Services.CreateScope();
        var recipients = scope.ServiceProvider.GetRequiredService<IShareRecipients>();

        await agent.CallToolAsync("set_sharing", new { deck, person = alex, access = "write" });
        await agent.CallToolAsync("set_sharing", new { deck, person = mira, access = "write" });

        var logged = (await recipients.DistinctSinceAsync(agent.PresenterId, DateTimeOffset.MinValue)).Select(r => r.Email).ToList();
        logged.ShouldContain(alex);
        logged.ShouldContain(mira);
    }

    [Test]
    public async Task SetSharing_WhenPresenterPending_ShouldBeRefused()
    {
        string accessToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var pending = await TestPeople.SignInAsync(services, TestUser.NewPending());
            var oauth = await services.GetRequiredService<IOAuthClients>().RegisterAsync(new ClientRegistration("Claude", ["http://127.0.0.1:33418/callback"]));
            var mcpUrl = services.GetRequiredService<IPublicUrls>().Mcp;
            accessToken = (await services.GetRequiredService<IAgentTokens>().IssueAsync(new CodeGrant(oauth.ClientId, pending.Id, "decks", mcpUrl))).AccessToken;
        }
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"set_sharing","arguments":{"deck":"nOsUcHdEcK00","person":"a@team.example","access":"read"}}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string Fresh(string name) => $"{name}-{Guid.NewGuid().ToString("N")[..8]}@team.example";
}
