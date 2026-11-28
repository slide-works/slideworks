using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// The audience side of /hubs/deck (J4) against fake sessions, relay and roster, so each test is green on its own:
// JoinAudience, LeaveAudience, WatchAudience, the disconnect hook, and that audience events reach the right group over
// every transport. Sam owns every deck.
[TestFixture]
[CancelAfter(30000)]
public class AudienceHubContractTests : ApiTestFixture
{
    private readonly List<IAsyncDisposable> _connections = [];
    private int _codes = 100000;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
        services.AddSingleton<RecordingRoster>().AddSingleton<IAudienceRoster>(provider => provider.GetRequiredService<RecordingRoster>());
        services.AddSingleton<SwitchableLimiter>().AddSingleton<IJoinLookupLimiter>(provider => provider.GetRequiredService<SwitchableLimiter>());
    };

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    private RecordingRoster Roster => Factory.Services.GetRequiredService<RecordingRoster>();

    [TearDown]
    public async Task DisconnectAsync()
    {
        Factory.Services.GetRequiredService<SwitchableLimiter>().Open = true;
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    [Test]
    public async Task JoinAudience_WhenNoGuestCookie_ShouldThrowNoGuest()
    {
        var (deck, code) = await LiveDeckAsync();
        var guest = await GuestAsync(noCookie: true);

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code));

        refused.Message.ShouldContain("no_guest", Case.Sensitive);
        Roster.Joins.ShouldNotContain(join => join.Deck == deck);
    }

    [Test]
    public async Task JoinAudience_WhenUnknownCode_ShouldThrowNoSession()
    {
        var guest = await GuestAsync();

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "999999"));

        refused.Message.ShouldContain("no_session", Case.Sensitive);
    }

    [Test]
    public async Task JoinAudience_WhenOutOfMisses_ShouldThrowTooManyAttemptsEvenForAKnownCode()
    {
        var (_, code) = await LiveDeckAsync();
        var guest = await GuestAsync();
        Factory.Services.GetRequiredService<SwitchableLimiter>().Open = false;

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code));

        refused.Message.ShouldContain("too_many_attempts", Case.Sensitive);
    }

    [Test]
    public async Task JoinAudience_WhenUnknownCode_ShouldCountAMiss()
    {
        var guest = await GuestAsync();
        var misses = Factory.Services.GetRequiredService<SwitchableLimiter>().Misses;

        await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "999998"));

        Factory.Services.GetRequiredService<SwitchableLimiter>().Misses.ShouldBe(misses + 1);
    }

    [Test]
    public async Task WatchAudience_WhenNotOwner_ShouldThrowDeckNotFound()
    {
        var (deck, _) = await LiveDeckAsync();
        var alex = await Track(HubReceiver.StartAsync(Factory, TestUser.Alex));
        var anonymous = await Track(HubReceiver.StartAsync(Factory, TestUser.Anonymous));

        var asAlex = await Should.ThrowAsync<HubException>(() => alex.Connection.InvokeAsync("WatchAudience", deck));
        var asAnonymous = await Should.ThrowAsync<HubException>(() => anonymous.Connection.InvokeAsync("WatchAudience", deck));

        asAlex.Message.ShouldContain(DeckHub.DECK_NOT_FOUND, Case.Sensitive);
        asAnonymous.Message.ShouldContain(DeckHub.DECK_NOT_FOUND, Case.Sensitive);
    }

    [Test]
    public async Task JoinAudience_WhenWebSockets_ShouldReceiveState()
    {
        var (joined, pushed) = await ReceiveStateAsync(HttpTransportType.WebSockets);

        // The return value is the AudienceState itself, written with its "type" discriminator.
        joined.GetProperty("type").GetString().ShouldBe("audienceState");
        joined.GetProperty("seq").GetInt64().ShouldBe(41);
        joined.GetProperty("phase").GetString().ShouldBe("live");
        pushed.GetProperty("type").GetString().ShouldBe("audienceState");
        pushed.GetProperty("seq").GetInt64().ShouldBe(42);
    }

    [Test]
    public async Task JoinAudience_WhenServerSentEvents_ShouldReceiveState()
    {
        var (joined, pushed) = await ReceiveStateAsync(HttpTransportType.ServerSentEvents);

        // The return value is the AudienceState itself, written with its "type" discriminator.
        joined.GetProperty("type").GetString().ShouldBe("audienceState");
        joined.GetProperty("seq").GetInt64().ShouldBe(41);
        joined.GetProperty("phase").GetString().ShouldBe("live");
        pushed.GetProperty("type").GetString().ShouldBe("audienceState");
        pushed.GetProperty("seq").GetInt64().ShouldBe(42);
    }

    [Test]
    public async Task JoinAudience_WhenLongPolling_ShouldReceiveState()
    {
        var (joined, pushed) = await ReceiveStateAsync(HttpTransportType.LongPolling);

        // The return value is the AudienceState itself, written with its "type" discriminator.
        joined.GetProperty("type").GetString().ShouldBe("audienceState");
        joined.GetProperty("seq").GetInt64().ShouldBe(41);
        joined.GetProperty("phase").GetString().ShouldBe("live");
        pushed.GetProperty("type").GetString().ShouldBe("audienceState");
        pushed.GetProperty("seq").GetInt64().ShouldBe(42);
    }

    // A refused call leaves nothing behind (J2's rule, applied to the hub): the refused connection is in no group, so an
    // event for the deck never reaches it. The bus delivers in order, so once the probe deck's event arrives, the refused
    // deck's earlier event would already have.
    [Test]
    public async Task JoinAudience_WhenRelayHasNoState_ShouldThrowNoSessionAndChangeNothing()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "No state yet");
        var code = Interlocked.Increment(ref _codes).ToString();
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set(code, deck, "live-session", AudiencePhase.Live);
        var (probeDeck, probeCode) = await LiveDeckAsync();
        var owner = await WatcherAsync(deck);
        var guest = await JoinedGuestAsync(probeCode);

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code));
        await Bus.PublishAsync(State(deck, 9, AudiencePhase.Live));
        await Bus.PublishAsync(State(probeDeck, 10, AudiencePhase.Live));

        refused.Message.ShouldContain("no_session", Case.Sensitive);
        // The real feed may publish a state for the probe deck first, so read up to the marker and check the refused deck's
        // event is not among what came before it.
        var seen = new List<long>();
        long seq;
        do
        {
            seq = (await guest.NextAudienceAsync()).GetProperty("seq").GetInt64();
            seen.Add(seq);
        } while (seq != 10);
        seen.ShouldNotContain(9);
        Roster.Joins.ShouldNotContain(join => join.Deck == deck);
        // The owner watches the deck: a joinedCount it got would be the refused join's.
        await Bus.PublishAsync(new JoinedCount(deck, 77));
        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(77);
    }

    [Test]
    public async Task JoinAudience_WhenOutOfMisses_ShouldAddNoGroupMembership()
    {
        var (deck, code) = await LiveDeckAsync();
        var (probeDeck, probeCode) = await LiveDeckAsync();
        var guest = await JoinedGuestAsync(probeCode);
        Factory.Services.GetRequiredService<SwitchableLimiter>().Open = false;

        await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code));
        await Bus.PublishAsync(State(deck, 9, AudiencePhase.Live));
        await Bus.PublishAsync(State(probeDeck, 10, AudiencePhase.Live));

        var seen = new List<long>();
        long seq;
        do
        {
            seq = (await guest.NextAudienceAsync()).GetProperty("seq").GetInt64();
            seen.Add(seq);
        } while (seq != 10);
        seen.ShouldNotContain(9);
        Roster.Joins.ShouldNotContain(join => join.Deck == deck);
    }

    [Test]
    public async Task JoinAudience_WhenUnknownCode_ShouldAddNoGroupMembership()
    {
        var (probeDeck, probeCode) = await LiveDeckAsync();
        var guest = await JoinedGuestAsync(probeCode);

        await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "999997"));
        await Bus.PublishAsync(State(probeDeck, 10, AudiencePhase.Live));

        var seen = new List<long>();
        long seq;
        do
        {
            seq = (await guest.NextAudienceAsync()).GetProperty("seq").GetInt64();
            seen.Add(seq);
        } while (seq != 10);
        seen.ShouldContain(10);
    }

    [Test]
    public async Task WatchAudience_WhenNotOwner_ShouldAddNoGroupMembership()
    {
        var (deck, _) = await LiveDeckAsync();
        using var alexClient = ClientFor(TestUser.Alex);
        var alexsDeck = await SharingApi.CreateDeckAsync(alexClient, "Alex's deck");
        var alex = await Track(HubReceiver.StartAsync(Factory, TestUser.Alex));
        await alex.Connection.InvokeAsync("WatchAudience", alexsDeck);

        await Should.ThrowAsync<HubException>(() => alex.Connection.InvokeAsync("WatchAudience", deck));
        await Bus.PublishAsync(new JoinedCount(deck, 5));
        await Bus.PublishAsync(new JoinedCount(alexsDeck, 6));

        (await alex.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(6);
    }

    [Test]
    public async Task JoinAudience_WhenLobby_ShouldFollowAndSitOnTheRosterWithNoSession()
    {
        var (deck, code) = await LiveDeckAsync(AudiencePhase.Lobby);
        var guest = await GuestAsync();

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await Bus.PublishAsync(State(deck, 2, AudiencePhase.Live));

        joined.GetProperty("phase").GetString().ShouldBe("lobby");
        (await guest.NextAudienceAsync()).GetProperty("phase").GetString().ShouldBe("live");
        Roster.Joins.Where(join => join.Deck == deck).ShouldBe([(deck, null, guest.GuestId)]);
    }

    [Test]
    public async Task JoinAudience_WhenLive_ShouldCountTheGuestAndTellTheOwnerOnly()
    {
        var (deck, code) = await LiveDeckAsync();
        var owner = await WatcherAsync(deck);
        var guest = await GuestAsync();

        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        var count = await owner.NextAudienceAsync();
        count.GetProperty("type").GetString().ShouldBe("joinedCount");
        count.GetProperty("count").GetInt32().ShouldBe(1);
        Roster.Joins.Where(join => join.Deck == deck).ShouldBe([(deck, "live-session", guest.GuestId)]);
        // The guest's next frame is the state published after the count: it never received the count.
        await Bus.PublishAsync(State(deck, 2, AudiencePhase.Live));
        (await guest.NextAudienceAsync()).GetProperty("type").GetString().ShouldBe("audienceState");
    }

    [Test]
    public async Task Publish_WhenStateOrEnded_ShouldReachGuestsOfThatDeckOnly()
    {
        var (deckA, codeA) = await LiveDeckAsync();
        var (deckB, codeB) = await LiveDeckAsync();
        var onA = await JoinedGuestAsync(codeA);
        var onB = await JoinedGuestAsync(codeB);
        var owner = await WatcherAsync(deckA);

        await Bus.PublishAsync(State(deckA, 5, AudiencePhase.Live));
        await Bus.PublishAsync(new AudienceEnded(deckB));

        (await onA.NextAudienceAsync()).GetProperty("seq").GetInt64().ShouldBe(5);
        (await onB.NextAudienceAsync()).GetProperty("type").GetString().ShouldBe("audienceEnded");
        // The owner watches counts only: the next thing it sees is the count that follows, not a state. The second since the
        // guests' joins has passed, so that count is not held back.
        Clock.Advance(TimeSpan.FromSeconds(1));
        await Bus.PublishAsync(new JoinedCount(deckA, 9));
        (await owner.NextAudienceAsync()).GetProperty("type").GetString().ShouldBe("joinedCount");
    }

    [Test]
    public async Task Publish_WhenReadingIn_ShouldReachTheOwnerOnly()
    {
        var (deck, code) = await LiveDeckAsync();
        var guest = await JoinedGuestAsync(code);
        var owner = await WatcherAsync(deck);

        await Bus.PublishAsync(new ReadingIn(deck, [new ReadingInCount("en", 1)]));
        await Bus.PublishAsync(State(deck, 6, AudiencePhase.Live));

        var reading = await owner.NextAudienceAsync();
        reading.GetProperty("type").GetString().ShouldBe("readingIn");
        reading.GetProperty("counts")[0].GetProperty("code").GetString().ShouldBe("en");
        // The guest's next frame is the state published after it: it never received Reading in.
        (await guest.NextAudienceAsync()).GetProperty("seq").GetInt64().ShouldBe(6);
    }

    [Test]
    public async Task LeaveAudience_WhenCalled_ShouldStopTheEventsAndLeaveTheRoster()
    {
        var (deck, code) = await LiveDeckAsync();
        var (otherDeck, otherCode) = await LiveDeckAsync();
        var guest = await JoinedGuestAsync(code);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", otherCode);

        await guest.Connection.InvokeAsync("LeaveAudience", code);
        await Bus.PublishAsync(State(deck, 7, AudiencePhase.Live));
        await Bus.PublishAsync(State(otherDeck, 8, AudiencePhase.Live));

        (await guest.NextAudienceAsync()).GetProperty("seq").GetInt64().ShouldBe(8);
        Roster.Leaves.ShouldContain(guest.Connection.ConnectionId!);
    }

    [Test]
    public async Task Disconnect_WhenGuestLeaves_ShouldCallRosterLeave()
    {
        var (deck, code) = await LiveDeckAsync();
        var owner = await WatcherAsync(deck);
        var guest = await JoinedGuestAsync(code);
        await owner.NextAudienceAsync();
        var connectionId = guest.Connection.ConnectionId!;
        Roster.SetCount(deck, 0);
        // The second since the join's count has passed, so the leave's count is not held back.
        Clock.Advance(TimeSpan.FromSeconds(1));

        await guest.DisposeAsync();

        var count = await owner.NextAudienceAsync();
        count.GetProperty("count").GetInt32().ShouldBe(0);
        Roster.Leaves.ShouldContain(connectionId);
    }

    // At most one joinedCount a second per deck: the first goes at once, the rest of that second collapse to the latest.
    [Test]
    public async Task Publish_WhenCountsInOneSecond_ShouldSendTheFirstThenTheLatestAfterTheSecond()
    {
        var (deck, _) = await LiveDeckAsync();
        var (otherDeck, _) = await LiveDeckAsync();
        var owner = await WatcherAsync(deck, otherDeck);

        await Bus.PublishAsync(new JoinedCount(deck, 1));
        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(1);
        await Bus.PublishAsync(new JoinedCount(deck, 2));
        await Bus.PublishAsync(new JoinedCount(deck, 3));
        // Another deck's count goes through the same bus in order, so once it arrives the two above were handled.
        await Bus.PublishAsync(new JoinedCount(otherDeck, 40));
        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(40);
        Clock.Advance(TimeSpan.FromSeconds(1));

        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(3);
    }

    private async Task<(JsonElement Joined, JsonElement Pushed)> ReceiveStateAsync(HttpTransportType transport)
    {
        var (deck, code) = await LiveDeckAsync();
        Factory.Services.GetRequiredService<FakeAudienceRelay>().Set(State(deck, 41, AudiencePhase.Live));
        var guest = await GuestAsync(transport);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await Bus.PublishAsync(State(deck, 42, AudiencePhase.Live));
        return (joined, await NextStateWithSeqAsync(guest, 42));
    }

    // The next audience message whose seq is the given one. The real AudienceFeed still listens to deck events, so a state it
    // publishes for the deck can arrive before the bus event the test published; the messages before it are skipped.
    private static async Task<JsonElement> NextStateWithSeqAsync(GuestClient guest, long seq)
    {
        JsonElement message;
        do message = await guest.NextAudienceAsync();
        while (message.GetProperty("seq").GetInt64() != seq);
        return message;
    }

    // Routed to the primary's slide-state group, where JoinAudience puts a guest (SW-883 D11; these decks have no settings, so en).
    private static AudienceState State(string deck, long seq, AudiencePhase phase)
        => new AudienceState(deck, seq, phase, phase == AudiencePhase.Live ? new AudienceSlide(1, 2, "title", JsonDocument.Parse("""{"title":"Hi"}""").RootElement.Clone(), 0, 0, false) : null, 0, null, null, null)
        {
            Group = AudienceSlideGroups.Name(deck, "en"),
        };

    // A deck Sam owns, and a fresh code that finds it in the given phase (live by default) with the relay answering a state.
    private async Task<(string Deck, string Code)> LiveDeckAsync(AudiencePhase phase = AudiencePhase.Live)
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Live deck");
        var code = Interlocked.Increment(ref _codes).ToString();
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set(code, deck, phase == AudiencePhase.Live ? "live-session" : null, phase);
        Factory.Services.GetRequiredService<FakeAudienceRelay>().Set(State(deck, 1, phase));
        return (deck, code);
    }

    private async Task<GuestClient> GuestAsync(HttpTransportType transport = HttpTransportType.WebSockets, bool noCookie = false)
    {
        var guest = await GuestClient.ConnectAsync(Factory, transport, noCookie: noCookie);
        _connections.Add(guest);
        return guest;
    }

    private async Task<GuestClient> JoinedGuestAsync(string code)
    {
        var guest = await GuestAsync();
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    private async Task<HubReceiver> WatcherAsync(params string[] decks)
    {
        var owner = await Track(HubReceiver.StartAsync(Factory, SignedInAs));
        foreach (var deck in decks)
        {
            await owner.Connection.InvokeAsync("WatchAudience", deck);
        }
        return owner;
    }

    private async Task<HubReceiver> Track(Task<HubReceiver> receiver)
    {
        var started = await receiver;
        _connections.Add(started);
        return started;
    }

    private sealed class RecordingRoster : IAudienceRoster
    {
        private readonly Lock _gate = new();
        private readonly List<(string Deck, string? Session, string Guest)> _joins = [];
        private readonly List<string> _leaves = [];

        private Dictionary<string, int> Counts { get; } = [];

        public IReadOnlyList<(string Deck, string? Session, string Guest)> Joins
        {
            get
            {
                lock (_gate)
                {
                    return [.. _joins];
                }
            }
        }

        public IReadOnlyList<string> Leaves
        {
            get
            {
                lock (_gate)
                {
                    return [.. _leaves];
                }
            }
        }

        private Dictionary<string, string> Decks { get; } = [];

        public Task<int> JoinAsync(string deckId, string? sessionId, string guestId, string connectionId)
        {
            lock (_gate)
            {
                _joins.Add((deckId, sessionId, guestId));
                Decks[connectionId] = deckId;
                return Task.FromResult(_joins.Count(j => j.Deck == deckId));
            }
        }

        public Task<RosterLeave?> LeaveAsync(string connectionId)
        {
            lock (_gate)
            {
                _leaves.Add(connectionId);
                Placed.Remove(connectionId);
                if (!Decks.Remove(connectionId, out var deck))
                {
                    return Task.FromResult<RosterLeave?>(null);
                }
                return Task.FromResult<RosterLeave?>(new RosterLeave(deck, "live-session", Counts.GetValueOrDefault(deck)));
            }
        }

        public Task<string?> DeckOfAsync(string connectionId)
        {
            lock (_gate)
            {
                return Task.FromResult(Decks.GetValueOrDefault(connectionId));
            }
        }

        // The count the next Leave answers for a deck (Join answers the number of joins so far).
        public void SetCount(string deck, int count)
        {
            lock (_gate)
            {
                Counts[deck] = count;
            }
        }

        private Dictionary<string, string> Placed { get; } = [];

        public Task ChooseAsync(string connectionId, string language) => Task.CompletedTask;

        public Task<AudienceSeat?> PlaceAsync(string connectionId, string language)
        {
            lock (_gate)
            {
                if (!Decks.ContainsKey(connectionId))
                {
                    return Task.FromResult<AudienceSeat?>(null);
                }
                var before = new AudienceSeat(connectionId, null, Placed.GetValueOrDefault(connectionId));
                Placed[connectionId] = language;
                return Task.FromResult<AudienceSeat?>(before);
            }
        }

        public Task<IReadOnlyList<AudienceSeat>> SeatsAsync(string deckId)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<AudienceSeat>>([.. Decks.Where(d => d.Value == deckId).Select(d => new AudienceSeat(d.Key, null, Placed.GetValueOrDefault(d.Key)))]);
            }
        }

        public Task<int> CountAsync(string sessionId) => Task.FromResult(0);

        public Task<int> JoinedAsync(string sessionId) => Task.FromResult(0);

        public Task<IReadOnlyList<ReadingInCount>> ReadingAsync(string deckId) => Task.FromResult<IReadOnlyList<ReadingInCount>>([]);

        public Task OpenedAsync(string deckId, string sessionId) => Task.CompletedTask;

        public Task ClosedAsync(string deckId, string sessionId) => Task.CompletedTask;
    }

    private sealed class SwitchableLimiter : IJoinLookupLimiter
    {
        private int _misses;

        public bool Open { get; set; } = true;

        public int Misses => Volatile.Read(ref _misses);

        public bool Allow(string callerKey) => Open;

        public void Miss(string callerKey) => Interlocked.Increment(ref _misses);

        public void Hit(string callerKey)
        {
        }

        public bool OverMissLimit(string callerKey) => !Open;

        public void RecordMiss(string callerKey) => Interlocked.Increment(ref _misses);
    }
}
