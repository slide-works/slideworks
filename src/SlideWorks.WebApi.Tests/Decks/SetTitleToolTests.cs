using System.Text.Json;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// set_title renames a deck after it was made, with create_deck's rules; the slug follows the title.
[TestFixture]
public class SetTitleToolTests : ApiTestFixture
{
    [Test]
    public async Task SetTitle_WhenOwnerRenames_ShouldChangeTitleAndSlugAndRecordOneActivityEntry()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title: "Butts on Seats (visual)", author: "Joel");
        var before = (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("slides").GetArrayLength();

        var result = await SlideCalls.OkAsync(agent, "set_title", new { deck, title = "  Butts on Seats  " });

        result.GetProperty("title").GetString().ShouldBe("Butts on Seats");
        result.GetProperty("slug").GetString().ShouldBe("butts-on-seats");
        result.GetProperty("author").GetString().ShouldBe("Joel");
        var got = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        got.GetProperty("title").GetString().ShouldBe("Butts on Seats");
        got.GetProperty("author").GetString().ShouldBe("Joel");
        got.GetProperty("slides").GetArrayLength().ShouldBe(before);
        var play = JsonDocument.Parse(await Client.GetStringAsync($"/api/play/{deck}")).RootElement;
        play.GetProperty("title").GetString().ShouldBe("Butts on Seats");
        play.GetProperty("slug").GetString().ShouldBe("butts-on-seats");
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity[0].GetProperty("summary").GetString().ShouldBe("renamed the deck to “Butts on Seats”");
        activity[0].GetProperty("tool").GetString().ShouldBe("set_title");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    [Test]
    public async Task SetTitle_WhenSubtitleAndAuthorSent_ShouldSetOrClearThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, author: "Joel");

        var result = await SlideCalls.OkAsync(agent, "set_title", new { deck, title = "Ship smaller", subtitle = "A new line", author = "" });

        result.GetProperty("subtitle").GetString().ShouldBe("A new line");
        result.GetProperty("author").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("subtitle").GetString().ShouldBe("A new line");
    }

    [Test]
    public async Task SetTitle_WhenNotTheOwner_ShouldSayDeckNotFoundAndChangeNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);

        (await SlideCalls.ErrorAsync(alex, "set_title", new { deck, title = "Mine now" })).ShouldBe("Deck not found");

        (await SlideCalls.OkAsync(sam, "get_deck", new { deck })).GetProperty("title").GetString().ShouldBe("Ship smaller");
    }

    [TestCase("   ", "title: required")]
    [TestCase(null, "title: at most 120 characters")]
    public async Task SetTitle_WhenTitleIsBlankOrTooLong_ShouldRefuseAndChangeNothing(string? title, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        (await SlideCalls.ErrorAsync(agent, "set_title", new { deck, title = title ?? new string('x', 121) })).ShouldBe(expected);

        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("title").GetString().ShouldBe("Ship smaller");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task SetTitle_OnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_title", $$$"""{"deck":"{{{deck}}}","title":"Renamed"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").ValueKind.ShouldBe(JsonValueKind.Object);
        result.GetProperty("structuredContent").GetProperty("title").GetString().ShouldBe("Renamed");
    }
}
