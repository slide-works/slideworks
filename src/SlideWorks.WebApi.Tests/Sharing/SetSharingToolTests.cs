using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// set_sharing (K6, D6): the owner's agent changes who can open a deck under the same rules as the dialog, and the change
// shows in the deck's activity with the tool's name. The SDK binds tool arguments with its own serializer, so the level
// is checked on the raw JSON-RPC wire as an agent would send it.
[TestFixture]
public class SetSharingToolTests : ApiTestFixture
{
    private const string LEVEL_INVALID = "level: must be \"private\", \"link\" or \"named\"";

    [Test]
    public async Task SetSharing_WhenAgent_ShouldLogActivityWithToolName()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, level = "link" });

        isError.ShouldBeFalse(result.ToString());
        result.GetProperty("level").GetString().ShouldBe("link");
        result.GetProperty("link").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        var latest = (await DeckSeed.ActivityAsync(Client, deck))[0];
        latest.GetProperty("summary").GetString().ShouldBe("changed sharing: Private → Anyone with the link");
        latest.GetProperty("tool").GetString().ShouldBe("set_sharing");
        latest.GetProperty("actor").GetString().ShouldBe("Claude");
        latest.GetProperty("actorKind").GetString().ShouldBe("agent");
        latest.GetProperty("via").GetString().ShouldBe("mcp");
        var dialog = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        dialog.GetProperty("lastChange").GetProperty("actor").GetString().ShouldBe("Claude");
        dialog.GetProperty("lastChange").GetProperty("actorKind").GetString().ShouldBe("agent");
        dialog.GetProperty("lastChange").GetProperty("atLabel").GetString().ShouldBe("just now");
    }

    [Test]
    public async Task SetSharing_WhenOtherPresenter_ShouldSayDeckNotFound()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);
        await sam.CallToolAsync("set_sharing", new { deck, level = "named", viewers = new[] { TestUser.Alex.Email } });

        var other = await alex.CallToolAsync("set_sharing", new { deck, level = "link" });
        var badLevel = await alex.CallToolAsync("set_sharing", new { deck, level = "public" });
        var unknown = await sam.CallToolAsync("set_sharing", new { deck = "nOsUcHdEcK00", level = "link" });

        foreach (var (isError, json) in new[] { other, badLevel, unknown })
        {
            isError.ShouldBeTrue();
            json.GetString().ShouldBe("Deck not found");
        }
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("named");
    }

    [Test]
    public async Task SetSharing_WhenLinkOnTheWire_ShouldOpenThePlayerAndAnswerCamelCase()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"link"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.GetProperty("level").GetString().ShouldBe("link");
        McpWire.TextOf(result).ShouldContain("\"level\":\"link\"", Case.Sensitive);
        (await anonymous.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task SetSharing_WhenNamedOnTheWire_ShouldStoreNormalizedViewersAndKeepThemWhenLeftOut()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var alex = ClientFor(TestUser.Alex);

        var named = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"named","viewers":[" Alex@Team.Example "]}""");
        await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"link"}""");
        var back = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"named"}""");

        var structured = named.GetProperty("structuredContent");
        structured.GetProperty("level").GetString().ShouldBe("named");
        structured.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example"]);
        McpWire.TextOf(named).ShouldContain("\"level\":\"named\"", Case.Sensitive);
        back.GetProperty("structuredContent").GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example"]);
        (await alex.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("\"public\"")]
    [TestCase("\"Link\"")]
    [TestCase("\"NAMED\"")]
    [TestCase("\"\"")]
    public async Task SetSharing_WhenBadLevelOnTheWire_ShouldBeAToolErrorWithTheApiText(string level)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":{{level}}}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe(LEVEL_INVALID);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    // viewers comes in as raw JSON, so a value that isn't a list of strings gets the API's text, not the SDK's generic
    // binding error, and changes nothing.
    [TestCase("\"alex@team.example\"")]
    [TestCase("[1]")]
    [TestCase("[[\"alex@team.example\"]]")]
    [TestCase("{\"email\":\"alex@team.example\"}")]
    [TestCase("true")]
    public async Task SetSharing_WhenViewersNotAListOfStringsOnTheWire_ShouldBeAToolErrorWithTheApiText(string viewers)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"named","viewers":{{viewers}}}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe("viewers: must be a list of email addresses");
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("level").GetString().ShouldBe("private");
        sharing.GetProperty("viewers").GetArrayLength().ShouldBe(0);
    }

    // A null viewers is the same as leaving it out: the list is kept.
    [Test]
    public async Task SetSharing_WhenViewersNullOnTheWire_ShouldKeepTheList()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"named","viewers":["alex@team.example"]}""");

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"link","viewers":null}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("level").GetString().ShouldBe("link");
        sharing.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example"]);
    }

    [Test]
    public async Task SetSharing_WhenInvalidEmail_ShouldNameIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var (isError, json) = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = new[] { "alex@team.example", "mira at team" } });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("viewers: \"mira at team\" is not an email address");
    }
}
