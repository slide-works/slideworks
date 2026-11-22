using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// SW-555: set_parts replaces a deck's parts with create_deck's rules; slides keep their part numbers.
[TestFixture]
public class SetPartsToolTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => HookTemplates.Register;

    [Test]
    public async Task SetParts_WhenOwnerSendsObjects_ShouldShowInGetDeckAndRecordOneActivityEntry()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two"]);
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "divider", slots = new { title = "Hi" }, part = 2 });
        var parts = new object[] { new { name = "Autocomplete", @short = "Auto", caption = "2021–22" }, "Two", new { name = "Appendix", mark = "A" } };

        var result = await SlideCalls.OkAsync(agent, "set_parts", new { deck, parts });

        result.GetProperty("parts").GetArrayLength().ShouldBe(3);
        var got = (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("parts");
        got[0].GetProperty("name").GetString().ShouldBe("Autocomplete");
        got[0].GetProperty("short").GetString().ShouldBe("Auto");
        got[0].GetProperty("caption").GetString().ShouldBe("2021–22");
        got[2].GetProperty("mark").GetString().ShouldBe("A");
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("slides")[0].GetProperty("part").GetInt32().ShouldBe(2);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Count.ShouldBe(3);
        activity[0].GetProperty("summary").GetString().ShouldBe("set the parts (3)");
        activity[0].GetProperty("tool").GetString().ShouldBe("set_parts");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    [Test]
    public async Task SetParts_WhenASlideWouldLoseItsPart_ShouldNameTheSlideAndChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two", "Three"]);
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "divider", slots = new { title = "Hi" }, part = 3 });

        var error = await SlideCalls.ErrorAsync(agent, "set_parts", new { deck, parts = new[] { "One", "Two" } });

        error.ShouldContain("slide 1 (part 3)");
        error.ShouldContain("at least 3 parts");
        error.ShouldContain("update_slide");
        (await SlideCalls.ErrorAsync(agent, "set_parts", new { deck, parts = Array.Empty<string>() })).ShouldContain("update_slide cannot clear a slide's part");
        (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("parts").GetArrayLength().ShouldBe(3);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(2);
    }

    [Test]
    public async Task SetParts_WhenNotTheOwner_ShouldSayDeckNotFound()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam, parts: ["One"]);

        (await SlideCalls.ErrorAsync(alex, "set_parts", new { deck, parts = new[] { "X" } })).ShouldBe("Deck not found");

        (await SlideCalls.OkAsync(sam, "get_deck", new { deck })).GetProperty("parts")[0].GetProperty("name").GetString().ShouldBe("One");
    }

    [TestCase("""{"parts":["One",""]}""", "parts[1]: required")]
    [TestCase("""{"parts":["1","2","3","4","5","6","7","8","9"]}""", "parts: at most 8 items")]
    [TestCase("""{"parts":[{"name":"A","mark":"A"},"B"]}""", "parts[0]: a part with a mark must come after every numbered part")]
    [TestCase("""{"parts":[{"name":"A","extra":"x"}]}""", "parts[0].extra: unknown field")]
    [TestCase("""{}""", "parts: required")]
    public async Task SetParts_WhenInvalid_ShouldGiveCreateDecksTextAndChangeNothing(string json, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One"]);
        var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
        args["deck"] = deck;

        (await SlideCalls.ErrorAsync(agent, "set_parts", args)).ShouldBe(expected);

        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task SetParts_OnTheWire_ShouldSendAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One"]);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_parts", $$$"""{"deck":"{{{deck}}}","parts":["A",{"name":"B","caption":"2025 →"}]}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").ValueKind.ShouldBe(JsonValueKind.Object);
        result.GetProperty("structuredContent").GetProperty("parts").GetArrayLength().ShouldBe(2);
    }

    // add_slides checks part numbers before its write transaction; set_parts commits in that gap, and the write must
    // check again against the deck's current parts and refuse, leaving no slide on a removed part.
    [Test]
    public async Task AddSlides_WhenSetPartsShrinksTheListBeforeTheWrite_ShouldRefuseAndWriteNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two", "Three"]);
        Factory.Services.GetRequiredService<HookTemplates>().Arm(async () =>
            await SlideCalls.OkAsync(agent, "set_parts", new { deck, parts = new[] { "One" } }));

        var error = await SlideCalls.ErrorAsync(agent, "add_slides", new { deck, slides = new[] { new { layout = "divider", slots = new { title = "Hi" }, part = 3 } } });

        error.ShouldContain("part: must be between 1 and 1");
        var got = await SlideCalls.OkAsync(agent, "get_deck", new { deck });
        got.GetProperty("slides").GetArrayLength().ShouldBe(0);
        got.GetProperty("parts").GetArrayLength().ShouldBe(1);
    }
}
