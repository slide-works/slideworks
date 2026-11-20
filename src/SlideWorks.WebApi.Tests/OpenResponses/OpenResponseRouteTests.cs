using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Join;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// The SW-8 HTTP routes: a guest's GET and submits on a word-cloud or open-text slide, and the presenter's control view.
// A refused submit or control action writes nothing. The open round in word-cloud.json is the one a phone answers.
[TestFixture]
[CancelAfter(30000)]
public class OpenResponseRouteTests : JoinFixture
{
    private const string ROUND = "6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63";

    [Test]
    public async Task Get_WhenTheSlideIsAWordCloud_ShouldMatchTheContract()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/join/{CODE}/slides/1/response");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "word-cloud.json")));
        JsonShape.AssertMatches(JsonDocument.Parse(body).RootElement, sample.RootElement.GetProperty("open"), "word-cloud.json");
    }

    [Test]
    public async Task Words_WhenWellFormed_ShouldReturnTheStoredWords()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 9, "  Focused ", "Calm"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "word-cloud.json")));
        JsonShape.AssertMatches(JsonDocument.Parse(body).RootElement, sample.RootElement.GetProperty("stored"), "word-cloud.json");
        body.ShouldBe("""{"words":["Focused","Calm"]}""");
    }

    [Test]
    public async Task Answer_WhenWellFormed_ShouldReturnTheStoredText()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{CODE}/slides/1/answer")
        {
            Content = JsonContent.Create(new { round = "a3d81c5e-7f2b-4e96-b0c4-1d8e6f9a2b57", revision = 6, text = "A shared   definition of done " }),
        };
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={GuestCookie.NewId()}");

        using var response = await anonymous.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"text":"A shared definition of done"}""");
    }

    [Test]
    public async Task Words_WhenAWordIsProfane_ShouldRefuseAndLeaveTheRowCount()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await EntryCountAsync();

        using var response = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 9, "Calm", "Bollocks!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"profane"}""");
        (await EntryCountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Words_WhenThereAreTooManyWords_ShouldRefuseWithConflict()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await EntryCountAsync();

        using var response = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 9, "Calm", "Busy", "Focused", "Tired"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_words"}""");
        (await EntryCountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Words_WhenThereIsNoSession_ShouldRefuse()
    {
        Sessions.Clear(CODE);
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await EntryCountAsync();

        using var response = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 9, "Calm"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        (await EntryCountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Words_WhenTheRevisionIsStale_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await EntryCountAsync();

        using var response = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 8, "Calm"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"stale"}""");
        (await EntryCountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Words_WhenTheGuestPassesTheLimit_ShouldRefuseWithTooManyAnswers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var guest = GuestCookie.NewId();
        for (var i = 0; i < 30; i++)
        {
            using var allowed = await anonymous.SendAsync(Words(guest, ROUND, 9, "Calm"));
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await anonymous.SendAsync(Words(guest, ROUND, 9, "Calm"));
        using var otherGuest = await anonymous.SendAsync(Words(GuestCookie.NewId(), ROUND, 9, "Calm"));

        response.StatusCode.ShouldBe((HttpStatusCode)429);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_answers"}""");
        otherGuest.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Get_WhenThePresenterReadsTheControlView_ShouldMatchTheContract()
    {
        using var sam = ClientFor(TestUser.Sam);
        var (deckId, round) = await OpenLiveWordCloudAsync(sam, ("guest-a", "Focused"), ("guest-b", "focused"), ("guest-c", "Calm"));

        using var response = await sam.GetAsync($"/api/decks/{deckId}/slides/1/response");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "open-response-control.json")));
        JsonShape.AssertMatches(JsonDocument.Parse(body).RootElement, sample.RootElement.GetProperty("wordCloud"), "open-response-control.json");
        body.ShouldBe($$"""{"kind":"word-cloud","phase":"open","round":"{{round}}","number":1,"revision":0,"results":"live","opens":"arrival","revealed":true,"guests":3,"entries":[{"number":1,"text":"Focused","count":2,"hidden":false},{"number":2,"text":"Calm","count":1,"hidden":false}]}""");
    }

    [Test]
    public async Task Hide_WhenTheNumberIsUnknown_ShouldReturnNoEntry()
    {
        using var sam = ClientFor(TestUser.Sam);
        var (deckId, _) = await OpenLiveWordCloudAsync(sam, ("guest-a", "Focused"));
        var before = await EntryCountAsync();

        using var response = await sam.PostAsync($"/api/decks/{deckId}/slides/1/response/entries/99/hide", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_entry"}""");
        (await EntryCountAsync()).ShouldBe(before);
        await using var scope = Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenEntries.CountAsync(e => e.HiddenAt != null)).ShouldBe(0);
    }

    // A deck whose slide 1 is a word cloud, taken live by Sam, with the sessions fake pointed at that real session, the
    // slide's round opened through the open route, and one row per guest and word written into it.
    private async Task<(string DeckId, string Round)> OpenLiveWordCloudAsync(HttpClient sam, params (string Guest, string Word)[] entries)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deckId, "word-cloud", """{"question":"How do you feel?"}""");
        using var live = await sam.PostAsync($"/api/decks/{deckId}/live", null);
        live.EnsureSuccessStatusCode();
        Sessions.Set(CODE, deckId, (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString(), AudiencePhase.Live);
        using var opened = await sam.PostAsync($"/api/decks/{deckId}/slides/1/response/open", null);
        opened.EnsureSuccessStatusCode();
        var round = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("round").GetString()!;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OpenEntries.AddRange(entries.Select(entry => new OpenEntry
        {
            Id = Guid.NewGuid(),
            RoundId = Guid.Parse(round),
            GuestId = entry.Guest,
            Position = 0,
            Text = OpenTextRules.Collapse(entry.Word),
            TextKey = OpenTextRules.KeyOf(entry.Word),
        }));
        await db.SaveChangesAsync();
        return (deckId, round);
    }

    private static HttpRequestMessage Words(string guestId, string round, int revision, params string[] words)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{CODE}/slides/1/words")
        {
            Content = JsonContent.Create(new { round, revision, words }),
        };
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={guestId}");
        return request;
    }

    private async Task<int> EntryCountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenEntries.CountAsync();
    }
}
