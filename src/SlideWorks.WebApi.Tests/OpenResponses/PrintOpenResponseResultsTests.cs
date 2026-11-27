using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// The print view's word-cloud and open-text results: the newest opened round of each slide in a session, live or ended, as
// the big screen's records with hidden words and answers left out. Rounds and rows are seeded as the other open-response
// tests do; the deck is made and taken live through the public API.
[TestFixture]
[CancelAfter(30000)]
public class PrintOpenResponseResultsTests : ApiTestFixture
{
    private const string CLOUD = """{"question":"One word for how this sprint felt?"}""";
    private const string TEXT = """{"question":"What would help your team ship smaller changes?"}""";

    [Test]
    public async Task ReadAsync_WhenAWordCloudRanInTheLastTalk_ShouldCountWordsByGuest()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD));
        var round = await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "Focused"), ("b", "focused"), ("c", "Calm"), ("a", "Calm"), ("d", "Tired"));

        var read = await ReadAsync(deck);

        var cloud = read.WordClouds.ShouldHaveSingleItem();
        read.OpenTexts.ShouldBeEmpty();
        cloud.Slide.ShouldBe(1);
        cloud.Round.ShouldBe(round.ToString());
        cloud.Phase.ShouldBe(OpenResponsePhase.Closed);
        cloud.Revealed.ShouldBeTrue();
        cloud.Guests.ShouldBe(4);
        cloud.Words.Select(w => (w.Text, w.Count, w.Hidden)).ShouldBe([("Calm", 2, false), ("Focused", 2, false), ("Tired", 1, false)]);
    }

    [Test]
    public async Task ReadAsync_WhenTheSessionHasEnded_ShouldStillReturnItsWords()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD));
        await SeedRoundAsync(deck, 1, 1, closed: false, ("a", "Focused"), ("b", "Focused"));
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();

        var read = await ReadAsync(deck);

        read.WordClouds.ShouldHaveSingleItem().Words.Select(w => (w.Text, w.Count)).ShouldBe([("Focused", 2)]);
    }

    [Test]
    public async Task ReadAsync_WhenAnEntryWasHidden_ShouldLeaveItOut()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD), ("open-text", TEXT));
        await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "Focused"), ("b", "Rude"), ("c", "Calm"));
        await HideAsync(deck, 1, "b");
        await SeedRoundAsync(deck, 2, 1, closed: true, ("a", "Fine"), ("b", "Rude answer"));
        await HideAsync(deck, 2, "b");

        var read = await ReadAsync(deck);

        read.WordClouds.ShouldHaveSingleItem().Words.Select(w => w.Text).ShouldBe(["Calm", "Focused"]);
        read.OpenTexts.ShouldHaveSingleItem().Answers.Select(a => a.Text).ShouldBe(["Fine"]);
        JsonSerializer.Serialize(read, JsonSerializerOptions.Web).ShouldNotContain("Rude", Case.Sensitive);
    }

    [Test]
    public async Task ReadAsync_WhenTwoGuestsHoldAWordAndOneEntryWasHidden_ShouldKeepTheWordWithCountOne()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD));
        await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "Focused"), ("b", "Focused"));
        await HideAsync(deck, 1, "b");

        var read = await ReadAsync(deck);

        read.WordClouds.ShouldHaveSingleItem().Words.Select(w => (w.Text, w.Count)).ShouldBe([("Focused", 1)]);
    }

    [Test]
    public async Task ReadAsync_WhenTheSlideRanTwoRounds_ShouldReturnTheNewest()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD));
        await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "Old"));
        var newest = await SeedRoundAsync(deck, 1, 2, closed: false, ("a", "New"), ("b", "New"));

        var read = await ReadAsync(deck);

        var cloud = read.WordClouds.ShouldHaveSingleItem();
        cloud.Round.ShouldBe(newest.ToString());
        cloud.Number.ShouldBe(2);
        cloud.Phase.ShouldBe(OpenResponsePhase.Open);
        cloud.Words.Select(w => (w.Text, w.Count)).ShouldBe([("New", 2)]);
    }

    [Test]
    public async Task ReadAsync_WhenOpenTextHadAHiddenAnswer_ShouldKeepTheOthersNumbers()
    {
        var deck = await LiveDeckAsync(("open-text", TEXT));
        await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "More demos"), ("b", "Hidden one"), ("c", "A shared definition of done"));
        await HideAsync(deck, 1, "b");

        var read = await ReadAsync(deck);

        var text = read.OpenTexts.ShouldHaveSingleItem();
        text.Guests.ShouldBe(3);
        text.Answers.Select(a => (a.Number, a.Text, a.Hidden)).ShouldBe([(1, "More demos", false), (3, "A shared definition of done", false)]);
    }

    [Test]
    public async Task GetPrint_WhenAWordCloudRanInTheLastTalk_ShouldCarryItsWords()
    {
        var deck = await LiveDeckAsync(("word-cloud", CLOUD));
        await SeedRoundAsync(deck, 1, 1, closed: true, ("a", "Focused"), ("b", "Focused"), ("c", "Calm"));
        var token = Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Print);
        using var browser = ClientFor(TestUser.Anonymous);
        browser.DefaultRequestHeaders.Add("X-Render-Token", token);

        var response = await browser.GetAsync($"/api/render/{deck}/print");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, "deck-print.json", ScreenShapes.DeckPrint);
        var cloud = view.GetProperty("wordClouds").EnumerateArray().ShouldHaveSingleItem();
        cloud.GetProperty("type").GetString().ShouldBe("wordCloud");
        cloud.GetProperty("slide").GetInt32().ShouldBe(1);
        cloud.GetProperty("words")[0].GetProperty("text").GetString().ShouldBe("Focused");
        cloud.GetProperty("words")[0].GetProperty("count").GetInt32().ShouldBe(2);
        cloud.GetProperty("words")[1].GetProperty("text").GetString().ShouldBe("Calm");
    }

    // A live deck with one slide per (layout, slots), in order.
    private async Task<string> LiveDeckAsync(params (string Layout, string Slots)[] slides)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        foreach (var (layout, slots) in slides)
        {
            await DeckSeed.AddSlideAsync(agent, deck, layout, slots);
        }
        (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();
        return deck;
    }

    private async Task<PrintOpenResponses> ReadAsync(string deck)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.AudienceSessions.AsNoTracking().Where(s => s.DeckId == deck).OrderByDescending(s => s.StartedAt).Select(s => s.Id).FirstAsync();
        return await scope.ServiceProvider.GetRequiredService<IPrintOpenResponseResults>().ReadAsync(deck, session, CancellationToken.None);
    }

    // Adds round `number` to the slide at 1-based `slide`, opened, with one row per (guest, text) in arrival order.
    private async Task<Guid> SeedRoundAsync(string deck, int slide, int number, bool closed, params (string Guest, string Text)[] rows)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Slides.SingleAsync(s => s.DeckId == deck && s.DeletedAt == null && s.Position == slide - 1);
        var session = await db.AudienceSessions.Where(s => s.DeckId == deck).OrderByDescending(s => s.StartedAt).Select(s => s.Id).FirstAsync();
        var kind = row.Layout == "word-cloud" ? OpenResponseKind.WordCloud : OpenResponseKind.OpenText;
        var response = await db.OpenResponses.SingleOrDefaultAsync(r => r.SlideId == row.Id);
        if (response is null)
        {
            response = new OpenResponse { Id = Guid.NewGuid(), DeckId = deck, SlideId = row.Id, Kind = kind };
            db.OpenResponses.Add(response);
        }
        var now = Clock.GetUtcNow();
        var round = new OpenRound { Id = Guid.NewGuid(), OpenResponseId = response.Id, SessionId = session, Number = number, Revision = rows.Length, OpenedAt = now, ClosedAt = closed ? now : null };
        db.OpenRounds.Add(round);
        db.OpenEntries.AddRange(rows.Select((r, index) => new OpenEntry
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            GuestId = r.Guest,
            Position = kind == OpenResponseKind.OpenText ? 0 : index,
            Text = r.Text,
            TextKey = OpenTextRules.KeyOf(r.Text),
            Arrival = index + 1,
        }));
        await db.SaveChangesAsync();
        return round.Id;
    }

    private async Task HideAsync(string deck, int slide, string guest)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slideId = await db.Slides.Where(s => s.DeckId == deck && s.DeletedAt == null && s.Position == slide - 1).Select(s => s.Id).SingleAsync();
        var rows = await db.OpenEntries
            .Where(e => e.GuestId == guest && db.OpenRounds.Any(r => r.Id == e.RoundId && db.OpenResponses.Any(o => o.Id == r.OpenResponseId && o.SlideId == slideId)))
            .ToListAsync();
        foreach (var entry in rows)
        {
            entry.HiddenAt = Clock.GetUtcNow();
        }
        await db.SaveChangesAsync();
    }
}
