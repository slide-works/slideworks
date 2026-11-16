using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// L7 deep_dive on add_slide and update_slide (D4): setting it marks the slide, false clears it, and leaving it out keeps
// it. Each write records one activity entry, lowercase and actor-first, and publishes one DeckChanged.
[TestFixture]
public class DeepDiveFlagTests : ApiTestFixture
{
    private const string SLOTS = """{"quote":"Ship it","attribution":"Sam Rivera"}""";

    [Test]
    public async Task AddSlide_WhenDeepDive_ShouldMarkTheSlideAndSayItInTheActivity()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        await AddAsync(agent, deck, deepDive: true);
        await AddAsync(agent, deck, deepDive: null);

        (await DeepDivesAsync(agent, deck)).ShouldBe([true, false]);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity[1].GetProperty("summary").GetString().ShouldBe("added slide 1 (quote) as a deep dive");
        activity[0].GetProperty("summary").GetString().ShouldBe("added slide 2 (quote)");
    }

    [Test]
    public async Task UpdateSlide_WhenDeepDiveSetThenCleared_ShouldRecordOneEntryForEach()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var revision = (await AddAsync(agent, deck, deepDive: null)).GetProperty("revision").GetInt64();
        var before = (await DeckSeed.ActivityAsync(Client, deck)).Count;

        revision = (await UpdateAsync(agent, deck, revision, deepDive: true)).GetProperty("revision").GetInt64();
        (await DeepDivesAsync(agent, deck)).ShouldBe([true]);
        revision = (await UpdateAsync(agent, deck, revision, deepDive: null)).GetProperty("revision").GetInt64();
        (await DeepDivesAsync(agent, deck)).ShouldBe([true]);
        await UpdateAsync(agent, deck, revision, deepDive: false);
        (await DeepDivesAsync(agent, deck)).ShouldBe([false]);

        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Count.ShouldBe(before + 3);
        activity.Take(3).Select(e => e.GetProperty("summary").GetString()).ShouldBe([
            "updated slide 1 (quote) and cleared its deep-dive mark",
            "updated slide 1 (quote)",
            "updated slide 1 (quote) and marked it a deep dive",
        ]);
        activity[0].GetProperty("tool").GetString().ShouldBe("update_slide");
    }

    [Test]
    public async Task UpdateSlide_WhenDeepDiveSet_ShouldPublishOneDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var revision = (await AddAsync(agent, deck, deepDive: null)).GetProperty("revision").GetInt64();
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await UpdateAsync(agent, deck, revision, deepDive: true);

        (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        received.Reader.TryRead(out _).ShouldBeFalse();
    }

    private static async Task<JsonElement> AddAsync(McpTestClient agent, string deck, bool? deepDive)
    {
        using var slots = JsonDocument.Parse(SLOTS);
        return await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "quote", slots = slots.RootElement, deep_dive = deepDive });
    }

    private static async Task<JsonElement> UpdateAsync(McpTestClient agent, string deck, long revision, bool? deepDive)
    {
        using var slots = JsonDocument.Parse(SLOTS);
        return await SlideCalls.OkAsync(agent, "update_slide", new { deck, slide = 1, revision, slots = slots.RootElement, deep_dive = deepDive });
    }

    private static async Task<IReadOnlyList<bool>> DeepDivesAsync(McpTestClient agent, string deck)
        => (await SlideCalls.SlidesAsync(agent, deck)).Select(s => s.GetProperty("deep_dive").GetBoolean()).ToList();
}
