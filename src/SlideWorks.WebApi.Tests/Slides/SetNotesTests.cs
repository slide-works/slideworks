using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K6 and K7: set_notes sets many slides' notes as one change with one activity entry. The Edits_ tests cover all four
// edit tools (update_slide, move_slide, delete_slide, set_notes): owner only (K8) and one DeckChanged each (K9).
[TestFixture]
[CancelAfter(30000)]
public class SetNotesTests : ApiTestFixture
{
    [Test]
    public async Task SetNotes_WhenManySlides_ShouldUpdateAllAndLogOnce()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");

        var result = await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 3, text = "Close strong." }, new { slide = 1, text = "Open with the story." } } });

        result.EnumerateObject().Select(p => p.Name).ShouldBe(["slides", "revision"], ignoreOrder: true);
        result.GetProperty("slides").EnumerateArray().Select(n => n.GetInt32()).ShouldBe([3, 1]);
        result.GetProperty("revision").GetInt64().ShouldBe(4);
        var slides = await SlidesAsync(agent, deck);
        slides.Select(s => s.GetProperty("notes").ValueKind == JsonValueKind.Null ? null : s.GetProperty("notes").GetString())
            .ShouldBe(["Open with the story.", null, "Close strong."]);
        Revisions(slides).ShouldBe([4, 2, 4]);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Count.ShouldBe(5);
        activity[0].GetProperty("summary").GetString().ShouldBe("set notes on slides 3, 1");
        activity[0].GetProperty("tool").GetString().ShouldBe("set_notes");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    [Test]
    public async Task SetNotes_WhenTextEmpty_ShouldClearThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "Say hi." } } });

        await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "  " } } });

        (await SlidesAsync(agent, deck))[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("set notes on slide 1");
    }

    // All or nothing: one bad entry refuses the whole call, and the good entries before it are not kept.
    [Test]
    public async Task SetNotes_WhenAnEntryIsInvalid_ShouldNameItAndChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B");

        var range = await ErrorAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "x" }, new { slide = 3, text = "y" } } });
        var twice = await ErrorAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 2, text = "x" }, new { slide = 2, text = "y" } } });
        var tooLong = await ErrorAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = new string('n', 4001) } } });
        var none = await ErrorAsync(agent, "set_notes", new { deck, notes = Array.Empty<object>() });

        range.ShouldBe("notes[1].slide: must be between 1 and 2");
        twice.ShouldBe("notes[1].slide: slide 2 is listed twice");
        tooLong.ShouldBe("notes[0].text: at most 4000 characters");
        none.ShouldBe("notes: list at least one slide");
        var slides = await SlidesAsync(agent, deck);
        slides.ShouldAllBe(s => s.GetProperty("notes").ValueKind == JsonValueKind.Null);
        Revisions(slides).ShouldBe([1, 2]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(3);
    }

    // The notes argument is a list of objects, which the MCP SDK binds itself, so it is checked on the raw wire too.
    [Test]
    public async Task SetNotes_OnTheWire_ShouldBindNotesAndSendAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B");

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "set_notes",
            $$$"""{"deck":"{{{deck}}}","notes":[{"slide":2,"text":"Pause here."}]}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").ValueKind.ShouldBe(JsonValueKind.Object);
        McpWire.TextOf(result).ShouldBe("""{"slides":[2],"revision":3}""");
        (await SlidesAsync(agent, deck))[1].GetProperty("notes").GetString().ShouldBe("Pause here.");
    }

    [Test]
    public async Task Edits_WhenOtherPresenter_ShouldSayDeckNotFound()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckOfQuotesAsync(alexAgent, "A", "B");
        await using var samAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var slots = JsonDocument.Parse("""{ "quote": "Mine now.", "attribution": "Sam" }""");

        foreach (var deck in new[] { alexDeck, "nOsUcHdEcK00" })
        {
            (await ErrorAsync(samAgent, "update_slide", new { deck, slide = 1, revision = 1, slots = slots.RootElement })).ShouldBe("Deck not found");
            (await ErrorAsync(samAgent, "move_slide", new { deck, slide = 2, to = 1 })).ShouldBe("Deck not found");
            (await ErrorAsync(samAgent, "delete_slide", new { deck, slide = 1 })).ShouldBe("Deck not found");
            (await ErrorAsync(samAgent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "Mine." } } })).ShouldBe("Deck not found");
        }
        var slides = await SlidesAsync(alexAgent, alexDeck);
        Quotes(slides).ShouldBe(["A", "B"]);
        Revisions(slides).ShouldBe([1, 2]);
        slides.ShouldAllBe(s => s.GetProperty("notes").ValueKind == JsonValueKind.Null);
    }

    [Test]
    public async Task Edits_ShouldPublishDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B");
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await UpdateAsync(agent, deck, 1, 1, """{ "quote": "A2", "attribution": "Sam" }""");
        await OkAsync(agent, "move_slide", new { deck, slide = 2, to = 1 });
        await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "Hi." } } });
        await OkAsync(agent, "delete_slide", new { deck, slide = 2 });

        for (var i = 0; i < 4; i++)
        {
            (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        }
    }
}
