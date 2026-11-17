using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        // Every method the server can call on a client, not only the two the receiver keeps: a leak on any of them shows here.
        var received = new List<string>();
        foreach (var method in new[] { DeckHub.EVENT_METHOD, DeckHub.AUDIENCE_EVENT_METHOD, DeckHub.PRESENTATION_STATE_METHOD })
        {
            guest.Connection.On<JsonElement>(method, e =>
            {
                lock (received)
                {
                    received.Add(e.GetRawText());
                }
            });
        }

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        // Slide 2 is black; slide 3 is the deep dive that has notes, and it is on screen; then back to the title slide, which has notes too.
        await MoveAsync(deck, slide: 2, step: 1, black: true);
        await guest.NextAudienceAsync();
        await MoveAsync(deck, slide: 3);
        var deepDive = await guest.NextAudienceAsync();
        await MoveAsync(deck, slide: 1);
        await guest.NextAudienceAsync();
        var body = await anonymous.GetStringAsync($"/api/join/{code}");

        deepDive.GetProperty("current").GetProperty("layout").GetString().ShouldBe("quote");
        string[] frames;
        lock (received)
        {
            frames = [.. received, joined.GetRawText()];
        }
        frames.Length.ShouldBeGreaterThanOrEqualTo(4);
        foreach (var text in frames.Append(body))
        {
            var lower = text.ToLowerInvariant();
            lower.ShouldNotContain("notes");
            lower.ShouldNotContain("timer");
            text.ShouldNotContain("1234567");
            text.ShouldNotContain("with the story");
            text.ShouldNotContain("one batch");
            text.ShouldNotContain("one customer");
            text.ShouldNotContain(PresentingSeed.QUOTE_NOTES);
            text.ShouldNotContain(DEEP_DIVE_TEXT);
            text.ShouldNotContain(VIEWER);
            text.ShouldNotContain(TestUser.Sam.Email);
        }
        // The look in the GET body has a legitimate deepDivesOnDivider setting, so the deep-dive keywords are checked on frames only.
        foreach (var raw in frames)
        {
            raw.ShouldNotContain(deck);
            raw.ToLowerInvariant().ShouldNotContain("deepdive");
            raw.ToLowerInvariant().ShouldNotContain("skip");
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
    public async Task Join_WhenServerRestartedWithSessionOpenAndNoPresentation_ShouldBeLiveWithNoSlideStartNothingAndKeepFeedingGuests()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        // A restart: a new host over the same database, so the session is open there, with a fresh feed and an empty registry.
        var dbPath = Factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_DB_PATH"]!;
        var registry = new SettablePresentations();
        using var restarted = TestAppFactory.Create(dbPath, Clock, null, null, services =>
            services.AddSingleton(registry).AddSingleton<IPresentations>(registry));
        using var anonymous = restarted.CreateClient();
        await using var guest = await GuestClient.ConnectAsync(restarted);

        var looked = await anonymous.GetFromJsonAsync<JsonElement>($"/api/join/{code}");
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await NotifyAsync(restarted.Services, registry, deck, slide: 3);
        var next = await guest.NextAudienceAsync();

        looked.GetProperty("state").GetProperty("phase").GetString().ShouldBe("live");
        looked.GetProperty("state").GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        joined.GetProperty("phase").GetString().ShouldBe("live");
        joined.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        registry.StartCalls.ShouldBe(0);
        joined.GetProperty("seq").GetInt64().ShouldBeGreaterThanOrEqualTo(Clock.GetUtcNow().ToUnixTimeMilliseconds());
        next.GetProperty("phase").GetString().ShouldBe("live");
        next.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(3);
        next.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
        registry.StartCalls.ShouldBe(0);
    }

    [Test]
    public async Task AudienceState_WhenChangesArriveOutOfOrder_ShouldEndOnTheRegistrysNewestPosition()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var newer = SettablePresentations.At(deck, slide: 3, seq: 2);
        var older = SettablePresentations.At(deck, slide: 2, seq: 1);
        Presentations.Set(newer);
        var observers = Factory.Services.GetServices<IPresentationObserver>().ToList();

        // SW-4 notifies outside its lock, so the older change's notification lands after the newer one's.
        foreach (var observer in observers)
        {
            await observer.OnChangedAsync(newer);
            await observer.OnChangedAsync(older);
        }
        var first = await guest.NextAudienceAsync();
        var second = await guest.NextAudienceAsync();

        first.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(3);
        second.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(3);
        second.GetProperty("seq").GetInt64().ShouldBeGreaterThan(first.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task AudienceState_WhenManyChangesRaceOnOneDeck_ShouldReachGuestWithStrictlyIncreasingSeq()
    {
        const int CHANGES = 30;
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        await Task.WhenAll(Enumerable.Range(0, CHANGES).Select(i => Task.Run(() => MoveAsync(deck, slide: 1 + i % 3, step: i % 2))));
        var seqs = new List<long>();
        for (var i = 0; i < CHANGES; i++)
        {
            seqs.Add((await guest.NextAudienceAsync()).GetProperty("seq").GetInt64());
        }

        for (var i = 1; i < seqs.Count; i++)
        {
            seqs[i].ShouldBeGreaterThan(seqs[i - 1]);
        }
    }

    [Test]
    public async Task End_WhenAPublishIsInFlightThatReadTheSessionAsLive_ShouldMakeAudienceEndedTheLastEvent()
    {
        var (deck, code) = await GoLiveAsync(await SeedAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        // One publish holds the deck's gate having read the session as live, and stops inside the registry read.
        var held = Presentations.BlockNextGet();
        var move = Task.Run(() => MoveAsync(deck, slide: 2));
        await held;

        var ended = Task.Run(() => Client.DeleteAsync($"/api/decks/{deck}/live"));
        while ((await GetLiveAsync(deck)).GetProperty("live").GetBoolean())
        {
            await Task.Yield();
        }
        Presentations.ReleaseGet();
        await move;
        (await ended).EnsureSuccessStatusCode();
        // A second audienceEnded after everything has settled marks the end of what could have been in flight.
        await Factory.Services.GetRequiredService<IAudienceEvents>().PublishAsync(new AudienceEnded(deck));
        var types = new List<string>();
        while (types.Count(t => t == "audienceEnded") < 2)
        {
            types.Add((await guest.NextAudienceAsync()).GetProperty("type").GetString()!);
        }

        types.ShouldBe(["audienceState", "audienceEnded", "audienceEnded"]);
    }

    [Test]
    public async Task Seq_WhenTheSessionCyclesThroughLobbyLiveEndedAndLobbyUnderAFrozenClock_ShouldOnlyIncrease()
    {
        var deck = await SeedAsync();
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam))
        {
            await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
        }
        var code = (await GetLiveAsync(deck)).GetProperty("code").GetString()!;
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var seqs = new List<long>();

        seqs.Add(await JoinSeqAsync(guest, code, "lobby"));
        await GoLiveAsync(deck);
        seqs.Add((await guest.NextAudienceAsync()).GetProperty("seq").GetInt64());
        await MoveAsync(deck, slide: 2);
        seqs.Add((await guest.NextAudienceAsync()).GetProperty("seq").GetInt64());
        seqs.Add(await JoinSeqAsync(guest, code, "live"));
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        await guest.NextAudienceAsync();
        seqs.Add(await JoinSeqAsync(guest, code, "ended"));
        // Ended turns into lobby again two hours after the session ended: the one time the clock moves.
        Clock.Advance(TimeSpan.FromHours(3));
        seqs.Add(await JoinSeqAsync(guest, code, "lobby"));
        await GoLiveAsync(deck);
        seqs.Add((await guest.NextAudienceAsync()).GetProperty("seq").GetInt64());

        for (var i = 1; i < seqs.Count; i++)
        {
            seqs[i].ShouldBeGreaterThan(seqs[i - 1], $"seq {i}: {string.Join(", ", seqs)}");
        }
    }

    private static async Task<long> JoinSeqAsync(GuestClient guest, string code, string phase)
    {
        var answer = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        answer.GetProperty("phase").GetString().ShouldBe(phase);
        return answer.GetProperty("seq").GetInt64();
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
        // The clock does not move in this test, so only the one seq source keeps the first live state above the lobby answer.
        live.GetProperty("seq").GetInt64().ShouldBeGreaterThan(lobby.GetProperty("seq").GetInt64());
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
    private Task MoveAsync(string deck, int slide, int step = 0, bool black = false)
        => NotifyAsync(Factory.Services, Presentations, deck, slide, step, black);

    private static async Task NotifyAsync(IServiceProvider services, SettablePresentations registry, string deck, int slide, int step = 0, bool black = false)
    {
        var state = SettablePresentations.At(deck, slide, step, black);
        registry.Set(state);
        foreach (var observer in services.GetServices<IPresentationObserver>())
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
