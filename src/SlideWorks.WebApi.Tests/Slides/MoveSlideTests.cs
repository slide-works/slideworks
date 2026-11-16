using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K5 and K6: move_slide renumbers the slides in between and stamps every slide whose position changed with the new
// revision, in one transaction that serialises edits to the deck.
[TestFixture]
[CancelAfter(60000)]
public class MoveSlideTests : ApiTestFixture
{
    [Test]
    public async Task Move_WhenToEarlierPosition_ShouldRenumber()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C", "D");

        var moved = await OkAsync(agent, "move_slide", new { deck, slide = 4, to = 2 });

        moved.EnumerateObject().Select(p => p.Name).ShouldBe(["slide", "revision", "url"], ignoreOrder: true);
        moved.GetProperty("slide").GetInt32().ShouldBe(2);
        moved.GetProperty("revision").GetInt64().ShouldBe(5);
        moved.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#2");
        var slides = await SlidesAsync(agent, deck);
        Numbers(slides).ShouldBe([1, 2, 3, 4]);
        Quotes(slides).ShouldBe(["A", "D", "B", "C"]);
        Revisions(slides).ShouldBe([1, 5, 5, 5]);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity[0].GetProperty("summary").GetString().ShouldBe("moved slide 4 to 2");
        activity[0].GetProperty("tool").GetString().ShouldBe("move_slide");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    [Test]
    public async Task Move_WhenToLaterPosition_ShouldRenumber()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C", "D");

        var moved = await OkAsync(agent, "move_slide", new { deck, slide = 1, to = 3 });

        moved.GetProperty("slide").GetInt32().ShouldBe(3);
        var slides = await SlidesAsync(agent, deck);
        Numbers(slides).ShouldBe([1, 2, 3, 4]);
        Quotes(slides).ShouldBe(["B", "C", "A", "D"]);
        Revisions(slides).ShouldBe([5, 5, 5, 4]);
    }

    [TestCase(0, 1, "slide: must be between 1 and 3")]
    [TestCase(4, 1, "slide: must be between 1 and 3")]
    [TestCase(1, 0, "to: must be between 1 and 3")]
    [TestCase(1, 4, "to: must be between 1 and 3")]
    public async Task Move_WhenOutOfRange_ShouldReturnToolError(int slide, int to, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");

        var error = await ErrorAsync(agent, "move_slide", new { deck, slide, to });

        error.ShouldBe(expected);
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["A", "B", "C"]);
        Revisions(slides).ShouldBe([1, 2, 3]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(4);
    }

    // Ten agents, each in its own MCP session, move and delete on one deck at once. The per-deck lock (taking the deck's
    // next revision first) must leave live positions 1..n with no gaps, give each call its own revision and record
    // exactly one activity entry per call.
    [Test]
    public async Task MoveAndDelete_WhenTenAgentsAtOnce_ShouldKeepPositionsGaplessAndLogEachCall()
    {
        await using var seeder = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var quotes = Enumerable.Range(1, 15).Select(n => $"S{n:00}").ToArray();
        var deck = await DeckOfQuotesAsync(seeder, quotes);
        var agents = new List<McpTestClient>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                agents.Add(await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, $"Agent {i}"));
            }

            var results = await Task.WhenAll(agents.Select((agent, i) => i % 2 == 0
                ? OkAsync(agent, "delete_slide", new { deck, slide = 1 })
                : OkAsync(agent, "move_slide", new { deck, slide = 3, to = 1 })));

            results.Select(r => r.GetProperty("revision").GetInt64()).Order().ShouldBe(Enumerable.Range(16, 10).Select(n => (long)n));
            var slides = await SlidesAsync(seeder, deck);
            Numbers(slides).ShouldBe(Enumerable.Range(1, 10));
            Quotes(slides).Distinct().Count().ShouldBe(10);
            Quotes(slides).ShouldBeSubsetOf(quotes);
            Revisions(slides).ShouldAllBe(revision => revision <= 25);
            var activity = await DeckSeed.ActivityAsync(Client, deck);
            activity.Count.ShouldBe(1 + 15 + 10);
            activity.Count(e => e.GetProperty("tool").GetString() == "delete_slide").ShouldBe(5);
            activity.Count(e => e.GetProperty("tool").GetString() == "move_slide").ShouldBe(5);
            activity.Take(10).Select(e => e.GetProperty("actor").GetString()).ShouldBe(Enumerable.Range(0, 10).Select(i => $"Agent {i}"), ignoreOrder: true);
        }
        finally
        {
            foreach (var agent in agents)
            {
                await agent.DisposeAsync();
            }
        }
    }
}
