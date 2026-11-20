using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// The link slot is the one place an agent's text becomes an href, so its refusal is proven over the wire.
[TestFixture]
[CancelAfter(30000)]
public class LinksSlideTests : ApiTestFixture
{
    private const string GOOD = "https://example.com/talks/small-batches";

    [Test]
    public async Task AddSlide_WhenALinkIsJavascript_ShouldRefuseAndAddNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "links", Slots(GOOD));
        var before = (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength();

        using var slots = JsonDocument.Parse(Slots("javascript:alert(document.cookie)"));
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout = "links", slots = slots.RootElement });

        isError.ShouldBeTrue(json.ToString());
        json.GetString()!.ShouldStartWith("links.links[0].url: ");
        (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength().ShouldBe(before);
    }

    [Test]
    public async Task UpdateSlide_WhenALinkBecomesJavascript_ShouldRefuseAndKeepTheOldAddress()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var added = await DeckSeed.AddSlideAsync(agent, deck, "links", Slots(GOOD));
        var revision = added.GetProperty("revision").GetInt64();

        using var slots = JsonDocument.Parse(Slots("javascript:alert(1)"));
        var (isError, json) = await agent.CallToolAsync("update_slide", new { deck, slide = 1, revision, slots = slots.RootElement });

        isError.ShouldBeTrue(json.ToString());
        json.GetString()!.ShouldStartWith("links.links[0].url: ");
        var slide = (await GetDeckAsync(agent, deck)).GetProperty("slides")[0];
        slide.GetProperty("slots").GetProperty("links")[0].GetProperty("url").GetString().ShouldBe(GOOD);
    }

    private static string Slots(string url) =>
        $$"""{ "headline": "Where to read more", "links": [{ "kind": "Talk", "title": "Small batches", "url": "{{url}}" }] }""";

    private static async Task<JsonElement> GetDeckAsync(McpTestClient agent, string deck)
    {
        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }
}
