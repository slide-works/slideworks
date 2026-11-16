using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// Every enum goes on the wire in camelCase, over HTTP and over MCP ("private", "link", "agent", "mcp"), never by its C#
// name and never as a number. These read the raw bytes, not a client's typed view of them.
[TestFixture]
public class EnumWireTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => EnumWireProbeController.AddTo;

    [Test]
    public async Task Http_ShouldSendEnumsInCamelCase()
    {
        var response = await Client.GetAsync($"/{EnumWireProbeController.PATH}");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldContain("\"level\":\"link\"");
        body.ShouldContain("\"actorKind\":\"agent\"");
        body.ShouldContain("\"via\":\"mcp\"");
    }

    [Test]
    public async Task CreateDeck_OnTheWire_ShouldSendSharingInCamelCase()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_deck", """{"title":"Ship smaller"}""");

        McpWire.TextOf(result).ShouldContain("\"sharing\":\"private\"");
        result.GetProperty("structuredContent").GetProperty("sharing").GetString().ShouldBe("private");
    }

    [Test]
    public async Task GetDeck_OnTheWire_ShouldSendSharingInCamelCase()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck", JsonSerializer.Serialize(new { deck }));

        McpWire.TextOf(result).ShouldContain("\"sharing\":\"private\"");
    }
}
