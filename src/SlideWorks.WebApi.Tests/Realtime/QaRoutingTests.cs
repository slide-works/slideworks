using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// SW-9's part of the audience channel (Q3, Q5, Q7), over real transports with the real feed, relay, hub, sessions and Go
// live and End. The Q&A seams are the settable fakes: FakeAudienceRoom says what the room may do, FakeQaRounds records what
// opens and closes the Q&A round, FakeReactions records the end of a session. Events are put on the audience bus as
// QuestionPublisher and Reactions would, so these tests prove the routing, not the publishers. Frames are read raw.
// "Never receives" is proved with a marker published after the event: the relay forwards in order, so a connection whose
// first Q&A event is the marker never got what came before it.
[TestFixture]
[CancelAfter(60000)]
public class QaRoutingTests : PollTestFixture
{
    // A waiting question's text: it is in the presenter's control and must never reach a phone.
    private const string WAITING = "Who reviews the slices?";

    // The keys a room may carry to a guest (qa-room.json, Q1): nothing about questions, guests or votes.
    private static readonly string[] _roomKeys = ["round", "questions", "names", "reactions", "listRevision"];

    private FakeAudienceRoom Room => Factory.Services.GetRequiredService<FakeAudienceRoom>();

    private FakeQaRounds Rounds => Factory.Services.GetRequiredService<FakeQaRounds>();

    private FakeReactions Reactions => Factory.Services.GetRequiredService<FakeReactions>();

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceRoom.AddTo(services);
        FakeQaRounds.AddTo(services);
        FakeReactions.AddTo(services);
        // Registered last, so the app's IQaRounds and IReactions are the probe, which passes every call on to the fakes.
        services.AddSingleton<OrderProbe>();
        services.AddSingleton<IQaRounds>(provider => provider.GetRequiredService<OrderProbe>());
        services.AddSingleton<IReactions>(provider => provider.GetRequiredService<OrderProbe>());
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task Frames_WhenLive_ShouldCarryExactlyTheRoomKeys(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        Room.Set(poll.DeckId, new QaRoom(round, QaRoomQuestions.Closed, false, true, 57));
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await MoveAsync(poll.DeckId, poll.OtherSlide);
        var pushed = await guest.NextAudienceAsync();
        var view = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);

        foreach (var frame in new[] { joined, pushed, view })
        {
            var room = frame.GetProperty("room");
            room.EnumerateObject().Select(p => p.Name).ShouldBe(_roomKeys, ignoreOrder: true);
            JsonShape.AssertMatches(room, QaRoomSample("open"), "qa-room.json open", ScreenShapes.QaRoom);
            room.GetProperty("round").GetString().ShouldBe(round);
            room.GetProperty("questions").GetString().ShouldBe("closed");
            room.GetProperty("names").GetBoolean().ShouldBeFalse();
            room.GetProperty("reactions").GetBoolean().ShouldBeTrue();
            room.GetProperty("listRevision").GetInt64().ShouldBe(57);
            frame.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        }
        pushed.GetProperty("phase").GetString().ShouldBe("live");
    }

    // The feed reads the room only while live: the fake answers for this deck in every phase, so a lobby or an ended state
    // that carried it would show here.
    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task Frames_WhenNotLive_ShouldCarryANullRoom(HttpTransportType transport)
    {
        var (deck, code) = await CreateDeckAsync();
        Room.Set(deck, new QaRoom(Guid.NewGuid().ToString(), QaRoomQuestions.Open, true, true, 3));
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);

        var lobby = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();
        var live = await guest.NextAudienceAsync();
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        var ended = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        lobby.GetProperty("phase").GetString().ShouldBe("lobby");
        lobby.GetProperty("room").ValueKind.ShouldBe(JsonValueKind.Null);
        live.GetProperty("phase").GetString().ShouldBe("live");
        live.GetProperty("room").ValueKind.ShouldBe(JsonValueKind.Object);
        ended.GetProperty("phase").GetString().ShouldBe("ended");
        ended.GetProperty("room").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task QuestionControl_WhenPublished_ShouldReachTheWatchGroupAndNoGuest(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);

        await Bus.PublishAsync(new QuestionControl(poll.DeckId, ControlOf(round)));
        await Bus.PublishAsync(new QuestionTally(poll.DeckId, round, 8, [new QuestionVotes(1, 3)]));
        var toOwner = await HubReceiver.NextQaEventAsync(owner.NextAudienceAsync);
        var guestFirst = await HubReceiver.NextQaEventAsync(guest.NextAudienceAsync);

        toOwner.EnumerateObject().Select(p => p.Name).ShouldBe(["type", "control"], ignoreOrder: true);
        toOwner.GetProperty("type").GetString().ShouldBe("questionControl");
        JsonShape.AssertMatches(toOwner.GetProperty("control"), QaControlSample("live"), "qa-control.json live", ScreenShapes.QaControl);
        toOwner.GetProperty("control").GetProperty("round").GetString().ShouldBe(round);
        toOwner.GetProperty("control").GetProperty("questions")[1].GetProperty("text").GetString().ShouldBe(WAITING);
        toOwner.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        // The guest's first Q&A frame is the tally published after the control: the control, which holds every question,
        // waiting and hidden ones too, went to the presenter's watch only.
        guestFirst.GetProperty("type").GetString().ShouldBe("questionTally");
        guestFirst.GetRawText().ShouldNotContain(WAITING, Case.Sensitive);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task Reactions_WhenPublished_ShouldReachTheWatchGroupAndNoGuest(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);

        await Bus.PublishAsync(new ReactionsBurst(poll.DeckId, poll.Slide, new ReactionCounts(3, 1, 0, 0, 2)));
        await Bus.PublishAsync(new QuestionTally(poll.DeckId, round, 8, [new QuestionVotes(1, 3)]));
        var toOwner = await HubReceiver.NextQaEventAsync(owner.NextAudienceAsync);
        var guestFirst = await HubReceiver.NextQaEventAsync(guest.NextAudienceAsync);

        JsonShape.AssertMatches(toOwner, AudienceEventSample("reactions"), "audience-events.json reactions", ScreenShapes.ReactionsBurst);
        toOwner.GetProperty("type").GetString().ShouldBe("reactions");
        toOwner.GetProperty("slide").GetInt32().ShouldBe(poll.Slide);
        var counts = toOwner.GetProperty("counts");
        new[] { "thumbsUp", "heart", "laugh", "wow", "clap" }.Select(e => counts.GetProperty(e).GetInt32()).ShouldBe([3, 1, 0, 0, 2]);
        toOwner.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        // The guest's first Q&A frame is the tally published after the reactions: no reaction reaches a phone.
        guestFirst.GetProperty("type").GetString().ShouldBe("questionTally");
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task QuestionTally_WhenPublished_ShouldReachEveryGuest(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var first = await GuestClient.ConnectAsync(Factory, transport);
        await using var second = await GuestClient.ConnectAsync(Factory, transport);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await second.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);

        await Bus.PublishAsync(new QuestionTally(poll.DeckId, round, 57, [new QuestionVotes(1, 14), new QuestionVotes(3, 6)]));
        await Bus.PublishAsync(new QuestionControl(poll.DeckId, ControlOf(round)));
        var toFirst = await HubReceiver.NextQaEventAsync(first.NextAudienceAsync);
        var toSecond = await HubReceiver.NextQaEventAsync(second.NextAudienceAsync);
        var ownerFirst = await HubReceiver.NextQaEventAsync(owner.NextAudienceAsync);

        foreach (var frame in new[] { toFirst, toSecond })
        {
            JsonShape.AssertMatches(frame, AudienceEventSample("questionTally"), "audience-events.json questionTally", ScreenShapes.QuestionTally);
            frame.GetProperty("type").GetString().ShouldBe("questionTally");
            frame.GetProperty("round").GetString().ShouldBe(round);
            frame.GetProperty("listRevision").GetInt64().ShouldBe(57);
            frame.GetProperty("votes").EnumerateArray().Select(v => (v.GetProperty("number").GetInt32(), v.GetProperty("votes").GetInt32())).ShouldBe([(1, 14), (3, 6)]);
            frame.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        }
        // The owner's watch gets the control published after the tally: the phones' tally goes to the audience group only.
        ownerFirst.GetProperty("type").GetString().ShouldBe("questionControl");
    }

    [Test]
    public async Task GoLive_WhenStarted_ShouldOpenTheQaRoundOnce()
    {
        var (deck, _) = await CreateDeckAsync();

        var first = await Client.PostAsync($"/api/decks/{deck}/live", null);
        var again = await Client.PostAsync($"/api/decks/{deck}/live", null);

        first.EnsureSuccessStatusCode();
        again.EnsureSuccessStatusCode();
        var session = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString();
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString().ShouldBe(session);
        Rounds.CallsFor(deck).ShouldBe([$"Open {session}"]);
    }

    [Test]
    public async Task End_WhenLive_ShouldCloseTheQaRoundAndEndReactions()
    {
        var poll = await SeedPollAsync();

        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();
        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();

        // The second End finds no open session, so it closes and ends nothing more.
        Rounds.CallsFor(poll.DeckId).ShouldBe([$"Open {poll.SessionId}", $"Close {poll.SessionId}"]);
        Reactions.EndsOf(poll.SessionId).ShouldBe(1);
    }

    // D1: the round is opened before the first live state is composed, so that state already carries it. The feed reads the
    // room only when it composes a live state, so no read may come before OpenAsync.
    [Test]
    public async Task GoLive_WhenStarted_ShouldOpenTheQaRoundBeforeTheRoomIsRead()
    {
        var (deck, _) = await CreateDeckAsync();
        var probe = Factory.Services.GetRequiredService<OrderProbe>();
        probe.Watch(deck, sessionId: null);

        (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();

        probe.RoomReadsAtOpen.ShouldBe([0]);
        Room.ReadsOf(deck).ShouldBeGreaterThan(0);
    }

    // The Q&A round is closed and the reactions written while the session is still live, before End writes EndedAt.
    [Test]
    public async Task End_WhenLive_ShouldEndTheQaRoundAndReactionsBeforeTheSessionEnds()
    {
        var poll = await SeedPollAsync();
        var probe = Factory.Services.GetRequiredService<OrderProbe>();
        probe.Watch(poll.DeckId, poll.SessionId);

        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();

        probe.Ends.ShouldBe([("Close", AudiencePhase.Live), ("EndSession", AudiencePhase.Live)]);
    }

    private async Task<(string Deck, string Code)> CreateDeckAsync()
    {
        string deck;
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            deck = await DeckSeed.CreateDeckAsync(agent);
            // A join slide puts the deck's session in the lobby before Go live.
            await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
            await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner");
        }
        var code = (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/live")).GetProperty("code").GetString()!;
        return (deck, code);
    }

    private async Task<HubReceiver> WatchAsync(string deckId, HttpTransportType transport)
    {
        var owner = await HubReceiver.StartAsync(Factory, SignedInAs, transport);
        await owner.Connection.InvokeAsync("WatchAudience", deckId);
        return owner;
    }

    // A live control with an open question and a waiting one, whose text must never reach a phone.
    private static QaControlView ControlOf(string round)
        => new(
            212,
            true,
            round,
            QaSettings.Default with { Review = true },
            new QaSlideView(2, true, true),
            [new QaSlideView(1, false, true)],
            new QaCounts(1, 1, 0, 0),
            1,
            [
                new QaQuestion(1, "How do you split a big change?", "Priya", "12 min", QuestionState.Open, false, true, 14),
                new QaQuestion(2, WAITING, "Sam K", "2 min", QuestionState.Waiting, false, false, 1),
            ],
            new ReactionCounts(41, 12, 7, 3, 28));

    // IQaRounds and IReactions over FakeQaRounds and FakeReactions, recording for one watched deck what the order tests need:
    // how many times the room had been read when OpenAsync ran, and the session's phase, read in a scope of its own, when
    // CloseAsync and EndSessionAsync ran. Every call is then passed on to the fakes, so the other tests read them as before.
    private sealed class OrderProbe(IServiceScopeFactory scopes, FakeQaRounds rounds, FakeReactions reactions, FakeAudienceRoom room) : IQaRounds, IReactions
    {
        private readonly Lock _gate = new();
        private readonly List<int> _roomReadsAtOpen = [];
        private readonly List<(string Call, AudiencePhase? Phase)> _ends = [];
        private string? _deckId;
        private Guid? _sessionId;

        public IReadOnlyList<int> RoomReadsAtOpen
        {
            get
            {
                lock (_gate)
                {
                    return [.. _roomReadsAtOpen];
                }
            }
        }

        public IReadOnlyList<(string Call, AudiencePhase? Phase)> Ends
        {
            get
            {
                lock (_gate)
                {
                    return [.. _ends];
                }
            }
        }

        // Records from now on for this deck and, when given, this session only; forgets what was recorded before.
        public void Watch(string deckId, Guid? sessionId)
        {
            lock (_gate)
            {
                _deckId = deckId;
                _sessionId = sessionId;
                _roomReadsAtOpen.Clear();
                _ends.Clear();
            }
        }

        public Task OpenAsync(string deckId, Guid sessionId)
        {
            lock (_gate)
            {
                if (deckId == _deckId)
                {
                    _roomReadsAtOpen.Add(room.ReadsOf(deckId));
                }
            }
            return rounds.OpenAsync(deckId, sessionId);
        }

        public async Task CloseAsync(string deckId, Guid sessionId)
        {
            await RecordEndAsync("Close", sessionId);
            await rounds.CloseAsync(deckId, sessionId);
        }

        public Task EnsureAsync(string deckId) => rounds.EnsureAsync(deckId);

        public Task<ReactionResult> TapAsync(string code, string guestId, string? emoji) => reactions.TapAsync(code, guestId, emoji);

        public Task<ReactionCounts> TotalsAsync(Guid sessionId) => reactions.TotalsAsync(sessionId);

        public async Task EndSessionAsync(Guid sessionId)
        {
            await RecordEndAsync("EndSession", sessionId);
            await reactions.EndSessionAsync(sessionId);
        }

        private async Task RecordEndAsync(string call, Guid sessionId)
        {
            string? deckId;
            lock (_gate)
            {
                deckId = sessionId == _sessionId ? _deckId : null;
            }
            if (deckId is null)
            {
                return;
            }
            await using var scope = scopes.CreateAsyncScope();
            var found = await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().ForDeckAsync(deckId);
            lock (_gate)
            {
                _ends.Add((call, found?.Phase));
            }
        }
    }

    private static JsonElement QaRoomSample(string name) => SampleOf("qa-room.json", name);

    private static JsonElement QaControlSample(string name) => SampleOf("qa-control.json", name);

    private static JsonElement AudienceEventSample(string name) => SampleOf("audience-events.json", name);

    private static JsonElement SampleOf(string file, string name)
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));
        return contract.RootElement.GetProperty(name).Clone();
    }
}
