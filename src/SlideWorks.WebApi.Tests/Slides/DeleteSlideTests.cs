using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K5 and K6: delete_slide is a soft delete. The slide stops showing everywhere, the later slides move up to close the gap
// and are stamped with the new revision.
[TestFixture]
[CancelAfter(30000)]
public class DeleteSlideTests : ApiTestFixture
{
    [Test]
    public async Task Delete_ShouldHideSlideAndCloseGap()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");

        var deleted = await OkAsync(agent, "delete_slide", new { deck, slide = 2 });

        deleted.EnumerateObject().Select(p => p.Name).ShouldBe(["deleted", "slides", "revision"], ignoreOrder: true);
        deleted.GetProperty("deleted").GetInt32().ShouldBe(2);
        deleted.GetProperty("slides").GetInt32().ShouldBe(2);
        deleted.GetProperty("revision").GetInt64().ShouldBe(4);
        var slides = await SlidesAsync(agent, deck);
        Numbers(slides).ShouldBe([1, 2]);
        Quotes(slides).ShouldBe(["A", "C"]);
        Revisions(slides).ShouldBe([1, 4]);
        var play = await Client.GetAsync($"/api/play/{deck}");
        play.StatusCode.ShouldBe(HttpStatusCode.OK, await play.Content.ReadAsStringAsync());
        var playSlides = (await play.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slides").EnumerateArray().ToList();
        Numbers(playSlides).ShouldBe([1, 2]);
        Quotes(playSlides).ShouldBe(["A", "C"]);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity[0].GetProperty("summary").GetString().ShouldBe("deleted slide 2 (quote)");
        activity[0].GetProperty("tool").GetString().ShouldBe("delete_slide");
        activity[0].GetProperty("actor").GetString().ShouldBe("Claude");
    }

    // add_slide counts only live slides, so a slide added after a delete takes the next number with no gap.
    [Test]
    public async Task Delete_ThenAdd_ShouldKeepNumbersGapless()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A", "B", "C");
        await OkAsync(agent, "delete_slide", new { deck, slide = 1 });

        var added = await DeckSeed.AddQuoteAsync(agent, deck, "D");
        var inserted = await DeckSeed.AddQuoteAsync(agent, deck, "E", position: 1);

        added.GetProperty("slide").GetInt32().ShouldBe(3);
        inserted.GetProperty("slide").GetInt32().ShouldBe(1);
        var slides = await SlidesAsync(agent, deck);
        Numbers(slides).ShouldBe([1, 2, 3, 4]);
        Quotes(slides).ShouldBe(["E", "B", "C", "D"]);
    }

    [Test]
    public async Task Delete_WhenOutOfRange_ShouldReturnToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");
        var empty = await DeckSeed.CreateDeckAsync(agent);

        var tooFar = await ErrorAsync(agent, "delete_slide", new { deck, slide = 2 });
        var none = await ErrorAsync(agent, "delete_slide", new { deck = empty, slide = 1 });

        tooFar.ShouldBe("slide: must be between 1 and 1");
        none.ShouldBe("slide: the deck has no slides");
        Quotes(await SlidesAsync(agent, deck)).ShouldBe(["A"]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(2);
    }
}
