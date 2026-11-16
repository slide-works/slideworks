using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// What the four edit tools (update_slide, move_slide, delete_slide, set_notes) share: owner only (K8), exactly one
// DeckChanged per edit and none for a refusal (K9), a refusal leaves the deck revision where it was, and a missing
// argument gets the tool's own error text rather than the MCP SDK's generic one.
[TestFixture]
[CancelAfter(30000)]
public class SlideEditsTests : ApiTestFixture
{
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

    // The decks library sorts by the deck's UpdatedAt and labels lastChange from the activity log, so every edit must set
    // both: an edit to an older deck puts it ahead of a deck created later, with the edit as its last change.
    [TestCase("update_slide", "updated slide 1 (quote)")]
    [TestCase("move_slide", "moved slide 2 to 1")]
    [TestCase("delete_slide", "deleted slide 1 (quote)")]
    [TestCase("set_notes", "set notes on slide 1")]
    public async Task Edits_WhenOlderDeckEdited_ShouldMoveItToTopOfLibrary(string tool, string summary)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var older = await DeckOfQuotesAsync(agent, "A", "B");
        Clock.Advance(TimeSpan.FromMinutes(5));
        var newer = await DeckSeed.CreateDeckAsync(agent, "Newer deck");
        Clock.Advance(TimeSpan.FromMinutes(5));
        using var slots = JsonDocument.Parse("""{ "quote": "A2", "attribution": "Sam" }""");
        object args = tool switch
        {
            "update_slide" => new { deck = older, slide = 1, revision = 1, slots = slots.RootElement },
            "move_slide" => new { deck = older, slide = 2, to = 1 },
            "delete_slide" => new { deck = older, slide = 1 },
            _ => new { deck = older, notes = new[] { new { slide = 1, text = "Hi." } } },
        };

        await OkAsync(agent, tool, args);

        var response = await Client.GetAsync("/api/decks");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("decks").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("id").GetString()).Take(2).ShouldBe([older, newer]);
        rows[0].GetProperty("lastChange").GetProperty("summary").GetString().ShouldBe(summary);
        rows[0].GetProperty("updatedLabel").GetString().ShouldBe("just now");
        rows[1].GetProperty("updatedLabel").GetString().ShouldBe("5 min ago");
    }

    [Test]
    public async Task Edits_ShouldPublishDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B");
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Events.Subscribe(null, e => received.Writer.WriteAsync(e).AsTask());

        await UpdateAsync(agent, deck, 1, 1, """{ "quote": "A2", "attribution": "Sam" }""");
        var afterUpdate = await DeckChangedSinceAsync(received.Reader, deck);
        await OkAsync(agent, "move_slide", new { deck, slide = 2, to = 1 });
        var afterMove = await DeckChangedSinceAsync(received.Reader, deck);
        await OkAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "Hi." } } });
        var afterNotes = await DeckChangedSinceAsync(received.Reader, deck);
        await OkAsync(agent, "delete_slide", new { deck, slide = 2 });
        var afterDelete = await DeckChangedSinceAsync(received.Reader, deck);

        new[] { afterUpdate, afterMove, afterNotes, afterDelete }.ShouldBe([1, 1, 1, 1]);
    }

    // A refused edit rolls back the revision it took, so the next edit gets the very next one; it publishes nothing and
    // records nothing.
    [Test]
    public async Task Edits_WhenRefused_ShouldPublishNothingAndKeepDeckRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B");
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Events.Subscribe(null, e => received.Writer.WriteAsync(e).AsTask());

        await UpdateErrorAsync(agent, deck, 1, 99, """{ "quote": "A2", "attribution": "Sam" }""");
        await UpdateErrorAsync(agent, deck, 1, 1, """{ "quote": "" }""");
        await ErrorAsync(agent, "move_slide", new { deck, slide = 9, to = 1 });
        await ErrorAsync(agent, "delete_slide", new { deck, slide = 9 });
        await ErrorAsync(agent, "set_notes", new { deck, notes = new[] { new { slide = 1, text = "x" }, new { slide = 9, text = "y" } } });
        var afterRefusals = await DeckChangedSinceAsync(received.Reader, deck);
        var moved = await OkAsync(agent, "move_slide", new { deck, slide = 2, to = 1 });

        afterRefusals.ShouldBe(0);
        moved.GetProperty("revision").GetInt64().ShouldBe(3);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1 + 2 + 1);
    }

    // On the raw wire, because the C# client never sends a call without an argument the tool's schema needs.
    [TestCase("set_notes", """{"deck":"{deck}"}""", "notes: required")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":null}""", "notes: required")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[null]}""", "notes[0]: must be an object with slide and text")]
    [TestCase("set_notes", """{"deck":"{deck}","notes":[{"slide":1,"text":"x"},null]}""", "notes[1]: must be an object with slide and text")]
    [TestCase("update_slide", """{"deck":"{deck}","slide":1,"revision":1}""", "slots: required")]
    [TestCase("update_slide", """{"deck":"{deck}","slide":1,"revision":1,"slots":null}""", "slots: required")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote"}""", "slots: required")]
    [TestCase("add_slide", """{"deck":"{deck}","layout":"quote","slots":null}""", "slots: required")]
    public async Task Edits_WhenArgumentMissingOnTheWire_ShouldReturnToolErrorText(string tool, string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, tool, argumentsJson.Replace("{deck}", deck, StringComparison.Ordinal));

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected);
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["A"]);
        Revisions(slides).ShouldBe([1]);
        slides[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    private IDeckEvents Events => Factory.Services.GetRequiredService<IDeckEvents>();

    // How many DeckChanged for deck arrived since the last call. Delivery keeps publish order per subscriber, so once a
    // marker event published now comes through, everything the finished calls published has come through before it.
    private async Task<int> DeckChangedSinceAsync(ChannelReader<DeckEvent> received, string deck)
    {
        var marker = $"marker-{Guid.NewGuid():N}";
        await Events.PublishAsync(new DeckChanged(marker));
        var count = 0;
        for (var next = await received.ReadAsync(); next.DeckId != marker; next = await received.ReadAsync())
        {
            if (next.DeckId == deck)
            {
                next.ShouldBeOfType<DeckChanged>();
                count++;
            }
        }
        return count;
    }
}
