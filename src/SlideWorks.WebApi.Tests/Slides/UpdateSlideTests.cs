using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K5 and K6: update_slide needs the revision get_deck gave. It re-validates (K2), recomputes steps, stamps the new
// revision and records one activity entry (K7). A stale revision, including an old number after a move or delete, is
// refused as a tool error and changes nothing.
[TestFixture]
[CancelAfter(30000)]
public class UpdateSlideTests : ApiTestFixture
{
    private const int RACE_ROUNDS = 5;

    [Test]
    public async Task Update_WhenRevisionCurrent_ShouldReplaceSlotsAndBumpRevision()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckOfQuotesAsync(agent, "A");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "kicker": "Why", "headline": "Old", "items": ["a"] }""");
        var read = (await SlidesAsync(agent, deck))[1];

        var updated = await UpdateAsync(agent, deck, 2, read.GetProperty("revision").GetInt64(), """
            { "kicker": null, "headline": "New", "items": ["x", "y", "z"], "reveal": "one-by-one" }
            """);

        updated.EnumerateObject().Select(p => p.Name).ShouldBe(["slide", "steps", "revision", "url", "warnings"], ignoreOrder: true);
        updated.GetProperty("slide").GetInt32().ShouldBe(2);
        updated.GetProperty("steps").GetInt32().ShouldBe(3);
        updated.GetProperty("revision").GetInt64().ShouldBe(3);
        updated.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller#2");
        var slides = await SlidesAsync(agent, deck);
        var slide = slides[1];
        slide.GetProperty("layout").GetString().ShouldBe("bullets");
        slide.GetProperty("steps").GetInt32().ShouldBe(3);
        slide.GetProperty("revision").GetInt64().ShouldBe(3);
        slide.GetProperty("slots").EnumerateObject().Select(p => p.Name).ShouldBe(["headline", "items", "reveal"]);
        slide.GetProperty("slots").GetProperty("headline").GetString().ShouldBe("New");
        Revisions(slides).ShouldBe([1, 3]);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity.Count.ShouldBe(4);
        activity[0].GetProperty("summary").GetString().ShouldBe("updated slide 2 (bullets)");
        activity[0].GetProperty("tool").GetString().ShouldBe("update_slide");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    [Test]
    public async Task Update_WhenLayoutAndPartGiven_ShouldChangeThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two"]);
        await DeckSeed.AddQuoteAsync(agent, deck, "A");

        await UpdateAsync(agent, deck, 1, 1, """{ "headline": "Now bullets", "items": ["a", "b"] }""", layout: "bullets", part: 2);

        var slide = (await SlidesAsync(agent, deck))[0];
        slide.GetProperty("layout").GetString().ShouldBe("bullets");
        slide.GetProperty("part").GetInt32().ShouldBe(2);
        slide.GetProperty("steps").GetInt32().ShouldBe(0);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("updated slide 1 (bullets)");
    }

    // The refused update rolls back the revision it took, so the next good update gets the very next one.
    [Test]
    public async Task Update_WhenRevisionStale_ShouldReturnToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        await UpdateAsync(agent, deck, 1, 1, """{ "quote": "B", "attribution": "Sam" }""");

        var error = await UpdateErrorAsync(agent, deck, 1, 1, """{ "quote": "C", "attribution": "Sam" }""");

        error.ShouldBe("Slide 1 changed since revision 1; read it again with get_deck");
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["B"]);
        Revisions(slides).ShouldBe([2]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(3);
        var next = await UpdateAsync(agent, deck, 1, 2, """{ "quote": "C", "attribution": "Sam" }""");
        next.GetProperty("revision").GetInt64().ShouldBe(3);
    }

    // On the raw JSON-RPC wire: the result is an object in structuredContent, and a stale revision is an isError result
    // carrying the conflict text.
    [Test]
    public async Task Update_OnTheWire_ShouldSendObjectAndStaleRevisionAsToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        var args = $$$"""{"deck":"{{{deck}}}","slide":1,"revision":1,"slots":{"quote":"B","attribution":"Sam"}}""";

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_slide", args);
        var stale = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_slide", args);

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.GetProperty("revision").GetInt64().ShouldBe(2);
        McpWire.TextOf(result).ShouldContain("\"slide\":1", Case.Sensitive);
        stale.GetProperty("isError").GetBoolean().ShouldBeTrue(stale.GetRawText());
        McpWire.TextOf(stale).ShouldBe("Slide 1 changed since revision 1; read it again with get_deck");
    }

    [Test]
    public async Task Update_WhenSlotInvalid_ShouldNamePath()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        var error = await UpdateErrorAsync(agent, deck, 1, 1, """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }, { "lead": "c", "text": "d" }, { "text": "e" }],
              "comes_back": [], "avoid": [] }
            """, layout: "practice");
        var image = await UpdateErrorAsync(agent, deck, 1, 1, """
            { "headline": "The board", "image": { "image": "img_3f9", "alt": "A taskboard" } }
            """, layout: "image");
        var part = await UpdateErrorAsync(agent, deck, 1, 1, """{ "quote": "B", "attribution": "Sam" }""", part: 1);

        error.ShouldBe("practice.do[2].lead: required");
        image.ShouldBe("image.image: unknown image img_3f9");
        part.ShouldBe("part: the deck has no parts");
        var slides = await SlidesAsync(agent, deck);
        slides[0].GetProperty("layout").GetString().ShouldBe("quote");
        Quotes(slides).ShouldBe(["A"]);
        Revisions(slides).ShouldBe([1]);
    }

    // C moves from 3 to 1, so slide 3 is now B and slide 2 is A, both re-stamped: neither old number with its old
    // revision matches any more.
    [Test]
    public async Task Update_AfterMoveWithOldNumberAndRevision_ShouldBeRefused()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");
        await OkAsync(agent, "move_slide", new { deck, slide = 3, to = 1 });

        var third = await UpdateErrorAsync(agent, deck, 3, 3, """{ "quote": "C2", "attribution": "Sam" }""");
        var first = await UpdateErrorAsync(agent, deck, 1, 1, """{ "quote": "A2", "attribution": "Sam" }""");

        third.ShouldBe("Slide 3 changed since revision 3; read it again with get_deck");
        first.ShouldBe("Slide 1 changed since revision 1; read it again with get_deck");
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["C", "A", "B"]);
        Revisions(slides).ShouldBe([4, 4, 4]);
    }

    // A is deleted, so slide 2 is now C (re-stamped) and there is no slide 3.
    [Test]
    public async Task Update_AfterDeleteWithOldNumberAndRevision_ShouldBeRefused()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");
        await OkAsync(agent, "delete_slide", new { deck, slide = 1 });

        var second = await UpdateErrorAsync(agent, deck, 2, 2, """{ "quote": "B2", "attribution": "Sam" }""");
        var third = await UpdateErrorAsync(agent, deck, 3, 3, """{ "quote": "C2", "attribution": "Sam" }""");

        second.ShouldBe("Slide 2 changed since revision 2; read it again with get_deck");
        third.ShouldBe("slide: must be between 1 and 2");
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["B", "C"]);
        Revisions(slides).ShouldBe([4, 4]);
    }

    // Ten agents read slide 2 at revision 2 and update it at once, for several rounds on a fresh deck each. Exactly one
    // wins; the rest are told the slide changed, and nothing of theirs is kept.
    [Test]
    [CancelAfter(120000)]
    public async Task Update_WhenTenAgentsRaceOnOneRevision_ShouldLetExactlyOneWin()
    {
        await using var seeder = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var agents = new List<McpTestClient>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                agents.Add(await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, $"Agent {i}"));
            }

            for (var round = 0; round < RACE_ROUNDS; round++)
            {
                var deck = await DeckOfQuotesAsync(seeder, "A", "B");
                var slots = agents.Select((_, i) => JsonDocument.Parse($$"""{ "quote": "Q{{i}}", "attribution": "Sam" }""")).ToList();

                var calls = await Task.WhenAll(agents.Select((agent, i) => agent.CallToolAsync("update_slide", new { deck, slide = 2, revision = 2L, slots = slots[i].RootElement })));

                calls.Count(call => !call.IsError).ShouldBe(1, string.Join(" | ", calls.Select(call => call.Json.ToString())));
                calls.Where(call => call.IsError).Select(call => call.Json.GetString())
                    .ShouldAllBe(text => text == "Slide 2 changed since revision 2; read it again with get_deck");
                var winner = calls.Single(call => !call.IsError).Json;
                winner.GetProperty("revision").GetInt64().ShouldBe(3);
                var slides = await SlidesAsync(seeder, deck);
                Revisions(slides).ShouldBe([1, 3]);
                Quotes(slides).Last()!.ShouldMatch("^Q[0-9]$");
                (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1 + 2 + 1);
                slots.ForEach(slot => slot.Dispose());
            }
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
