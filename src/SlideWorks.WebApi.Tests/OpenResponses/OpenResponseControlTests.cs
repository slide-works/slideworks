using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.OpenResponses.Mcp;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// The presenter's open, close, new round, hide and restore on a word-cloud slide, opening on arrival and closing on leaving,
// closing when the live session ends, and the agent's open. The deck is made and taken live through the public API; the
// round and its entries are rows this fixture writes, so no guest route runs. A refused action writes nothing.
[TestFixture]
[CancelAfter(30000)]
public class OpenResponseControlTests : ApiTestFixture
{
    private const int WORD_CLOUD_SLIDE = 2;

    [Test]
    public async Task Close_WhenTheRoundIsOpen_ShouldMarkItClosed()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3);

        using var response = await Client.PostAsync($"/api/decks/{live.DeckId}/slides/{WORD_CLOUD_SLIDE}/response/close", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stored = await RoundAsync(round);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("phase").GetString().ShouldBe("closed");
        stored.ClosedAt.ShouldBe(Clock.GetUtcNow());
        stored.Revision.ShouldBe(4);
    }

    [Test]
    public async Task Close_WhenTheRoundIsAlreadyClosed_ShouldRefuseAndChangeNothing()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var closedAt = Clock.GetUtcNow().AddMinutes(-5);
        var round = await SeedRoundAsync(live, closedAt, revision: 5);

        using var response = await Client.PostAsync($"/api/decks/{live.DeckId}/slides/{WORD_CLOUD_SLIDE}/response/close", null);

        var stored = await RoundAsync(round);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"wrong_phase"}""");
        stored.ClosedAt.ShouldBe(closedAt);
        stored.Revision.ShouldBe(5);
    }

    [Test]
    public async Task Hide_WhenThePresenterHidesAWord_ShouldHideEveryRowWithThatText()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3, ("guest-a", "Focused"), ("guest-b", "focused"), ("guest-c", "Calm"));

        using var response = await Client.PostAsync($"/api/decks/{live.DeckId}/slides/{WORD_CLOUD_SLIDE}/response/entries/1/hide", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rows = await EntriesAsync(round);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("entries")[0].GetProperty("hidden").GetBoolean().ShouldBeTrue();
        body.GetProperty("entries")[1].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        rows.Where(row => row.TextKey == "focused").ShouldAllBe(row => row.HiddenAt == Clock.GetUtcNow());
        rows.Single(row => row.TextKey == "calm").HiddenAt.ShouldBeNull();
        (await RoundAsync(round)).Revision.ShouldBe(4);
    }

    [Test]
    public async Task Hide_WhenTheNumberIsUnknown_ShouldReturnNoEntry()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3, ("guest-a", "Focused"));

        using var response = await Client.PostAsync($"/api/decks/{live.DeckId}/slides/{WORD_CLOUD_SLIDE}/response/entries/99/hide", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_entry"}""");
        (await EntriesAsync(round)).ShouldAllBe(row => row.HiddenAt == null);
        (await RoundAsync(round)).Revision.ShouldBe(3);
    }

    [Test]
    public async Task NewRound_WhenTheRoundIsClosed_ShouldOpenTheNextNumber()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var first = await SeedRoundAsync(live, Clock.GetUtcNow().AddMinutes(-5), revision: 4, ("guest-a", "Focused"));

        using var response = await Client.PostAsync($"/api/decks/{live.DeckId}/slides/{WORD_CLOUD_SLIDE}/response/new-round", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rounds = await RoundsAsync(first.OpenResponseId);
        var next = rounds.Single(r => r.Number == 2);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("phase").GetString().ShouldBe("open");
        body.GetProperty("number").GetInt32().ShouldBe(2);
        body.GetProperty("round").GetString().ShouldBe(next.Id.ToString());
        body.GetProperty("entries").GetArrayLength().ShouldBe(0);
        next.OpenedAt.ShouldBe(Clock.GetUtcNow());
        next.ClosedAt.ShouldBeNull();
        (await EntriesAsync(next)).ShouldBeEmpty();
        (await EntriesAsync(first)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Arrival_WhenThePresenterLeaves_ShouldCloseTheRound()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3);

        await Arrival.OnChangedAsync(SettablePresentations.At(live.DeckId, 1));

        var stored = await RoundAsync(round);
        stored.ClosedAt.ShouldBe(Clock.GetUtcNow());
        stored.Revision.ShouldBe(4);
    }

    [Test]
    public async Task Arrival_WhenTheSessionHasNoRound_ShouldOpenTheFirst()
    {
        var live = await GoLiveWithAWordCloudAsync();

        await Arrival.OnChangedAsync(SettablePresentations.At(live.DeckId, WORD_CLOUD_SLIDE));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var response = await db.OpenResponses.AsNoTracking().SingleAsync(r => r.SlideId == live.SlideId);
        var round = await db.OpenRounds.AsNoTracking().SingleAsync(r => r.OpenResponseId == response.Id);
        response.Kind.ShouldBe(OpenResponseKind.WordCloud);
        round.SessionId.ShouldBe(live.SessionId);
        round.Number.ShouldBe(1);
        round.OpenedAt.ShouldBe(Clock.GetUtcNow());
        round.ClosedAt.ShouldBeNull();
    }

    [Test]
    public async Task Arrival_WhenThePresenterComesBack_ShouldLeaveTheRoundClosed()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3);
        await Arrival.OnChangedAsync(SettablePresentations.At(live.DeckId, 1));

        await Arrival.OnChangedAsync(SettablePresentations.At(live.DeckId, WORD_CLOUD_SLIDE));

        var rounds = await RoundsAsync(round.OpenResponseId);
        rounds.Count.ShouldBe(1);
        rounds[0].ClosedAt.ShouldBe(Clock.GetUtcNow());
    }

    [Test]
    public async Task End_WhenTheSessionEnds_ShouldCloseTheRound()
    {
        var live = await GoLiveWithAWordCloudAsync();
        var round = await SeedRoundAsync(live, closedAt: null, revision: 3);

        using var response = await Client.DeleteAsync($"/api/decks/{live.DeckId}/live");

        var stored = await RoundAsync(round);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        stored.ClosedAt.ShouldBe(Clock.GetUtcNow());
        stored.Revision.ShouldBe(4);
    }

    [Test]
    public async Task OpenWordCloud_WhenTheDeckIsNotLive_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deckId, "Small batches ship sooner");
        await DeckSeed.AddSlideAsync(agent, deckId, "word-cloud", """{"question":"How do you feel?"}""");

        var (isError, json) = await agent.CallToolAsync(OpenWordCloudTool.NAME, new { deck = deckId, slide = WORD_CLOUD_SLIDE });

        isError.ShouldBeTrue(json.ToString());
        json.GetString()!.ShouldStartWith(OpenResponseControls.NOT_LIVE);
        await using var scope = Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenResponses.CountAsync(r => r.DeckId == deckId)).ShouldBe(0);
    }

    private OpenResponseArrival Arrival => Factory.Services.GetServices<IPresentationObserver>().OfType<OpenResponseArrival>().Single();

    // A deck whose slide 1 is a quote and slide 2 a word cloud, taken live by its owner, with that slide's id and the session's.
    private async Task<(string DeckId, Guid SlideId, Guid SessionId)> GoLiveWithAWordCloudAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deckId, "Small batches ship sooner");
        await DeckSeed.AddSlideAsync(agent, deckId, "word-cloud", """{"question":"How do you feel?"}""");
        using var response = await Client.PostAsync($"/api/decks/{deckId}/live", null);
        response.EnsureSuccessStatusCode();
        var sessionId = Guid.Parse((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!);
        await using var scope = Factory.Services.CreateAsyncScope();
        var slideId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == WORD_CLOUD_SLIDE - 1)
            .Select(s => s.Id)
            .SingleAsync();
        return (deckId, slideId, sessionId);
    }

    // Round 1 of the slide in the session, opened five minutes before the clock's now, with one row per guest and word.
    private async Task<OpenRound> SeedRoundAsync((string DeckId, Guid SlideId, Guid SessionId) live, DateTimeOffset? closedAt, int revision, params (string Guest, string Word)[] entries)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var response = new OpenResponse { Id = Guid.NewGuid(), DeckId = live.DeckId, SlideId = live.SlideId, Kind = OpenResponseKind.WordCloud };
        var round = new OpenRound
        {
            Id = Guid.NewGuid(),
            OpenResponseId = response.Id,
            SessionId = live.SessionId,
            Number = 1,
            Revision = revision,
            OpenedAt = Clock.GetUtcNow().AddMinutes(-5),
            ClosedAt = closedAt,
        };
        db.OpenResponses.Add(response);
        db.OpenRounds.Add(round);
        db.OpenEntries.AddRange(entries.Select(entry => new OpenEntry
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            GuestId = entry.Guest,
            Position = 0,
            Text = OpenTextRules.Collapse(entry.Word),
            TextKey = OpenTextRules.KeyOf(entry.Word),
        }));
        await db.SaveChangesAsync();
        return round;
    }

    private async Task<OpenRound> RoundAsync(OpenRound round)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenRounds.AsNoTracking().SingleAsync(r => r.Id == round.Id);
    }

    private async Task<List<OpenRound>> RoundsAsync(Guid openResponseId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenRounds.AsNoTracking().Where(r => r.OpenResponseId == openResponseId).ToListAsync();
    }

    private async Task<List<OpenEntry>> EntriesAsync(OpenRound round)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenEntries.AsNoTracking().Where(e => e.RoundId == round.Id).ToListAsync();
    }
}
