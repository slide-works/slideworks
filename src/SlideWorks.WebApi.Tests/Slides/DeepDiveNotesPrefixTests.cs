using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// D16: a deep dive's notes read with the skip prefix in get_deck and on the deck page while the flag is set. The stored
// notes never change, so clearing the flag gives them back as written, and the play view carries no notes at all.
[TestFixture]
public class DeepDiveNotesPrefixTests : ApiTestFixture
{
    private const string PREFIX = "Deep dive. Skip if short of time.";
    private const string NOTES = "Walk through the numbers.";

    [Test]
    public async Task DeepDive_ShouldPrefixTheNotesOnReadAndKeepTheStoredNotes()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, NOTES, deepDive: true);

        (await GetDeckNotesAsync(agent, deck)).ShouldBe($"{PREFIX} {NOTES}");
        (await PageNotesAsync(deck)).ShouldBe($"{PREFIX} {NOTES}");

        var revision = (await SlideCalls.SlidesAsync(agent, deck))[0].GetProperty("revision").GetInt64();
        using var slots = JsonDocument.Parse(Slots);
        await SlideCalls.OkAsync(agent, "update_slide", new { deck, slide = 1, revision, slots = slots.RootElement, deep_dive = false });

        (await GetDeckNotesAsync(agent, deck)).ShouldBe(NOTES);
        (await PageNotesAsync(deck)).ShouldBe(NOTES);
    }

    [Test]
    public async Task DeepDive_WhenNoNotes_ShouldReadAsThePrefixAlone()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, notes: null, deepDive: true);

        (await GetDeckNotesAsync(agent, deck)).ShouldBe(PREFIX);
        (await PageNotesAsync(deck)).ShouldBe(PREFIX);
    }

    [Test]
    public async Task DeepDive_ShouldBeMarkedInThePlayViewWithNoNotes()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await AddAsync(agent, deck, NOTES, deepDive: true);

        var slide = (await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}")).GetProperty("slides")[0];

        slide.GetProperty("deepDive").GetBoolean().ShouldBeTrue();
        slide.TryGetProperty("notes", out _).ShouldBeFalse();
    }

    private static string Slots => """{"quote":"Ship it","attribution":"Sam Rivera"}""";

    private static async Task AddAsync(McpTestClient agent, string deck, string? notes, bool deepDive)
    {
        using var slots = JsonDocument.Parse(Slots);
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "quote", slots = slots.RootElement, notes, deep_dive = deepDive });
    }

    private static async Task<string?> GetDeckNotesAsync(McpTestClient agent, string deck)
        => (await SlideCalls.SlidesAsync(agent, deck))[0].GetProperty("notes").GetString();

    private async Task<string?> PageNotesAsync(string deck)
        => (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}")).GetProperty("slides")[0].GetProperty("notes").GetString();
}
