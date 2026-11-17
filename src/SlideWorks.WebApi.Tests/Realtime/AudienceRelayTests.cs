using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Presenting;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// The real audience relay (S1b) with the real sessions, hub and bus, over real transports. SW-4's registry is stood in by a
// settable IPresentations, and a test plays its part by setting a state and calling the observers. Sam owns every deck.
[TestFixture]
[CancelAfter(60000)]
public class AudienceRelayTests : ApiTestFixture
{
    private const string VIEWER = "viewer.person@guests.example";
    private const string DEEP_DIVE_TEXT = "Hidden appendix about pricing 91c2";

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    private SettablePresentations Presentations => Factory.Services.GetRequiredService<SettablePresentations>();

    [Test]
    public void Observers_ShouldIncludeTheRelaysOnce()
    {
        var observers = Factory.Services.GetServices<IPresentationObserver>();

        observers.Count(o => o is AudienceRelayObserver).ShouldBe(1);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task AudienceState_WhenPresenterMoves_ShouldReachGuestWithIncreasingSeq(HttpTransportType transport)
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await MoveAsync(deck, slide: 2, step: 1);
        var first = await guest.NextAudienceAsync();
        await MoveAsync(deck, slide: 2, step: 2);
        var second = await guest.NextAudienceAsync();

        joined.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(1);
        first.GetProperty("type").GetString().ShouldBe("audienceState");
        first.GetProperty("phase").GetString().ShouldBe("live");
        first.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        first.GetProperty("current").GetProperty("step").GetInt32().ShouldBe(1);
        second.GetProperty("current").GetProperty("step").GetInt32().ShouldBe(2);
        first.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
        second.GetProperty("seq").GetInt64().ShouldBeGreaterThan(first.GetProperty("seq").GetInt64());
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task AudienceState_WhenSlideHasNotes_ShouldNeverSendNotesOrTimer(HttpTransportType transport)
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync(share: true));
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var frames = new List<JsonElement> { await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code) };
        await MoveAsync(deck, slide: 2, step: 1, black: true);
        frames.Add(await guest.NextAudienceAsync());
        await MoveAsync(deck, slide: 1);
        frames.Add(await guest.NextAudienceAsync());
        var body = await anonymous.GetStringAsync($"/api/join/{code}");

        body.ShouldNotContain(DEEP_DIVE_TEXT);
        body.ShouldNotContain(VIEWER);
        body.ShouldNotContain(TestUser.Sam.Email);
        body.ToLowerInvariant().ShouldNotContain("notes");
        foreach (var raw in frames.Select(f => f.GetRawText()))
        {
            raw.ToLowerInvariant().ShouldNotContain("deepdive");
            raw.ShouldNotContain(PresentingSeed.TITLE_NOTES.Split('\n')[0].Replace("**", string.Empty));
            raw.ShouldNotContain("one batch");
            raw.ShouldNotContain(PresentingSeed.QUOTE_NOTES);
            raw.ShouldNotContain(DEEP_DIVE_TEXT);
            raw.ShouldNotContain(VIEWER);
            raw.ShouldNotContain(TestUser.Sam.Email);
            raw.ShouldNotContain("1234567");
            raw.ToLowerInvariant().ShouldNotContain("notes");
            raw.ToLowerInvariant().ShouldNotContain("timer");
            raw.ShouldNotContain(deck);
        }
    }

    [Test]
    public async Task AudienceState_WhenPresenterIsOnADeepDive_ShouldSendTheSlideWithoutTheMark()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await MoveAsync(deck, slide: 4);
        var frame = await guest.NextAudienceAsync();

        frame.GetProperty("current").GetProperty("layout").GetString().ShouldBe("quote");
        frame.GetRawText().ToLowerInvariant().ShouldNotContain("deep");
    }

    [Test]
    public async Task AudienceState_WhenDeckEdited_ShouldCarryNewRevision()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        await DeckSeed.AddQuoteAsync(agent, deck, "One more slide");
        var frame = await guest.NextAudienceAsync();

        frame.GetProperty("revision").GetInt64().ShouldBeGreaterThan(joined.GetProperty("revision").GetInt64());
        frame.GetProperty("current").GetProperty("count").GetInt32().ShouldBe(joined.GetProperty("current").GetProperty("count").GetInt32() + 1);
        frame.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task End_WhenLive_ShouldSendAudienceEnded()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        await MoveAsync(deck, slide: 2);
        var ended = await guest.NextAudienceAsync();
        var after = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        ended.GetProperty("type").GetString().ShouldBe("audienceEnded");
        after.GetProperty("phase").GetString().ShouldBe("ended");
        after.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task JoinSlideShown_WhenTwice_ShouldStampOnce()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
        await DeckSeed.AddQuoteAsync(agent, deck, "After the join slide");
        var shown = Clock.GetUtcNow();

        await GoLiveAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(5));
        await MoveAsync(deck, slide: 2);
        await MoveAsync(deck, slide: 1);
        await MoveAsync(deck, slide: 1, step: 0, black: true);

        (await StampAsync(deck)).ShouldBe(shown);
    }

    [Test]
    public async Task JoinSlideShown_WhenNeverOnJoinSlide_ShouldNotStamp()
    {
        var (deck, _) = await GoLiveAsync(await SeedAsync());

        await MoveAsync(deck, slide: 2);

        (await StampAsync(deck)).ShouldBeNull();
    }

    [Test]
    public async Task Join_WhenRegistryLostItsStateWhileSessionStaysOpen_ShouldRestoreAndKeepFeedingGuests()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        Presentations.Drop(deck);
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await MoveAsync(deck, slide: 3);
        var next = await guest.NextAudienceAsync();

        joined.GetProperty("phase").GetString().ShouldBe("live");
        joined.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(1);
        next.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(3);
    }

    [Test]
    public async Task Started_WhenCalledAgain_ShouldBeSafeAndPublishAHigherSeq()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await Factory.Services.GetRequiredService<IAudienceRelay>().StartedAsync(deck);
        var again = await guest.NextAudienceAsync();

        again.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task AudienceState_WhenSessionIsNotLive_ShouldNotBePublishedOrCarryASlide()
    {
        var deck = await SeedAsync();
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam))
        {
            await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
        }
        var code = (await GetLiveAsync(deck)).GetProperty("code").GetString()!;
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var lobby = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await MoveAsync(deck, slide: 2);
        await GoLiveAsync(deck);
        var live = await guest.NextAudienceAsync();

        lobby.GetProperty("phase").GetString().ShouldBe("lobby");
        lobby.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        live.GetProperty("phase").GetString().ShouldBe("live");
        live.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
    }

    private async Task<string> SeedAsync(bool share = false)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await PresentingSeed.CreateDeckAsync(agent);
        var (isError, added) = await agent.CallToolAsync("add_slide", new { deck, layout = "quote", slots = new { quote = DEEP_DIVE_TEXT, attribution = "Sam" }, deep_dive = true });
        isError.ShouldBeFalse(added.ToString());
        if (share)
        {
            await SharingApi.PutOkAsync(Client, deck, "named", VIEWER);
        }
        return deck;
    }

    private async Task<(string Deck, string Code)> GoLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        return (deck, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!);
    }

    private async Task<JsonElement> GetLiveAsync(string deck)
        => await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/live");

    // What SW-4's registry does on an accepted change: record the state, then tell every observer.
    private async Task MoveAsync(string deck, int slide, int step = 0, bool black = false)
    {
        var state = SettablePresentations.At(deck, slide, step, black);
        Presentations.Set(state);
        foreach (var observer in Factory.Services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
    }

    private async Task<DateTimeOffset?> StampAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AudienceSessions.AsNoTracking().Where(s => s.DeckId == deck).Select(s => s.JoinSlideFirstShownAt).SingleAsync();
    }
}
