using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// Where a poll's state and events go (V3, V5), over real transports with the real feed, relay, hub and sessions. The poll
// seams are the settable fakes: FakeAudienceActivity says what is on screen and FakePollAnswers who may watch a round's
// results. Events are put on the audience bus as PollPublisher would, so these tests prove the routing, not the publisher.
// Frames are read raw. "Never receives" is proved with a marker published after the event: the relay forwards in order,
// so a connection whose first poll event is the marker never got what came before it.
[TestFixture]
[CancelAfter(60000)]
public class PollRoutingTests : PollTestFixture
{
    private const string OTHER_ROUND = "6f1d2c3b-0000-4000-8000-00000000beef";

    private FakeAudienceActivity Activity => Factory.Services.GetRequiredService<FakeAudienceActivity>();

    private FakePollAnswers Answers => Factory.Services.GetRequiredService<FakePollAnswers>();

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceActivity.AddTo(services);
        FakePollAnswers.AddTo(services);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task AudienceState_WhenSlideHasPoll_ShouldCarryActivity(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round));
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await MoveAsync(poll.DeckId, poll.Slide);
        var pushed = await guest.NextAudienceAsync();

        foreach (var frame in new[] { joined, pushed })
        {
            var activity = frame.GetProperty("activity");
            PollActivityKeys.Of(activity).ShouldBe(PollActivityKeys.All, ignoreOrder: true);
            JsonShape.AssertMatches(activity, PollActivitySample("open"), "poll-activity.json open", ScreenShapes.PollActivity);
            activity.GetProperty("type").GetString().ShouldBe("poll");
            activity.GetProperty("round").GetString().ShouldBe(round);
            activity.GetProperty("phase").GetString().ShouldBe("open");
            activity.GetProperty("pick").GetString().ShouldBe("upTo");
            activity.GetProperty("results").GetString().ShouldBe("afterAnswer");
            activity.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe(PollSeed.OPTIONS);
            frame.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        }
        pushed.GetProperty("phase").GetString().ShouldBe("live");
    }

    // The feed asks for the activity only while live: the fake answers for this deck in every phase, so a lobby or an
    // ended state that carried it would show here.
    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task AudienceState_WhenSessionIsInTheLobbyOrEnded_ShouldCarryNoActivity(HttpTransportType transport)
    {
        string deck;
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            deck = await DeckSeed.CreateDeckAsync(agent);
            await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
            await DeckSeed.AddSlideAsync(agent, deck, "poll", JsonSerializer.Serialize(new { question = PollSeed.QUESTION, options = PollSeed.OPTIONS }));
        }
        var code = (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/live")).GetProperty("code").GetString()!;
        Activity.Set(code, deck, ActivityOf(Guid.NewGuid().ToString()));
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);

        var lobby = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();
        var live = await guest.NextAudienceAsync();
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        var ended = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        lobby.GetProperty("phase").GetString().ShouldBe("lobby");
        lobby.GetProperty("activity").ValueKind.ShouldBe(JsonValueKind.Null);
        live.GetProperty("phase").GetString().ShouldBe("live");
        live.GetProperty("activity").ValueKind.ShouldBe(JsonValueKind.Object);
        ended.GetProperty("phase").GetString().ShouldBe("ended");
        ended.GetProperty("activity").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task PollTally_WhenTargetIsAudience_ShouldReachGuestsOnly(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);

        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Audience, round, 3, null));
        await Bus.PublishAsync(new PollControl(poll.DeckId, ControlOf(poll.Slide, round)));
        var toGuest = await HubReceiver.NextPollEventAsync(guest.NextAudienceAsync);
        var toOwner = await HubReceiver.NextPollEventAsync(owner.NextAudienceAsync);

        JsonShape.AssertMatches(toGuest, AudienceEventSample("pollTally"), "audience-events.json pollTally", ScreenShapes.PollTally);
        toGuest.GetProperty("type").GetString().ShouldBe("pollTally");
        toGuest.GetProperty("round").GetString().ShouldBe(round);
        toGuest.GetProperty("answered").GetInt32().ShouldBe(3);
        toGuest.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        toGuest.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        // The owner's first poll frame is the control published after the tally: the audience tally never reached it.
        toOwner.GetProperty("type").GetString().ShouldBe("pollControl");
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task PollTally_WhenTargetIsResults_ShouldReachOnlyGuestsWhoWatch(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var watcher = await GuestClient.ConnectAsync(Factory, transport);
        await using var other = await GuestClient.ConnectAsync(Factory, transport);
        await watcher.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await other.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);
        Answers.SetWatch(poll.Code, watcher.GuestId, round);

        await watcher.WatchPollAsync(poll.Code);
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, round, 5, [2, 3, 0]));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, OTHER_ROUND, 9, [9, 0, 0]));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Audience, round, 5, null));
        await Bus.PublishAsync(new PollControl(poll.DeckId, ControlOf(poll.Slide, round)));
        var watcherFirst = await HubReceiver.NextPollEventAsync(watcher.NextAudienceAsync);
        var watcherSecond = await HubReceiver.NextPollEventAsync(watcher.NextAudienceAsync);
        var otherFirst = await HubReceiver.NextPollEventAsync(other.NextAudienceAsync);
        var ownerFirst = await HubReceiver.NextPollEventAsync(owner.NextAudienceAsync);

        JsonShape.AssertMatches(watcherFirst, AudienceEventSample("pollTally"), "audience-events.json pollTally", ScreenShapes.PollTally);
        watcherFirst.GetProperty("round").GetString().ShouldBe(round);
        watcherFirst.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([2, 3, 0]);
        // Next is the audience marker: another round's results tally never reached this watcher.
        watcherSecond.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        // A guest who has not watched gets the marker first: no results tally, with its counts, reached it.
        otherFirst.GetProperty("type").GetString().ShouldBe("pollTally");
        otherFirst.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        otherFirst.GetRawText().ShouldNotContain("[", Case.Sensitive);
        ownerFirst.GetProperty("type").GetString().ShouldBe("pollControl");
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task PollControl_WhenPublished_ShouldReachOnlyThePresentersWatch(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        await using var watcher = await GuestClient.ConnectAsync(Factory, transport);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await watcher.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        Answers.SetWatch(poll.Code, watcher.GuestId, round);
        await watcher.WatchPollAsync(poll.Code);
        await using var owner = await WatchAsync(poll.DeckId, transport);

        await Bus.PublishAsync(new PollControl(poll.DeckId, ControlOf(poll.Slide, round)));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, round, 4, [1, 3, 0]));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Audience, round, 4, null));
        var toOwner = await HubReceiver.NextPollEventAsync(owner.NextAudienceAsync);
        var guestFirst = await HubReceiver.NextPollEventAsync(guest.NextAudienceAsync);
        var watcherFirst = await HubReceiver.NextPollEventAsync(watcher.NextAudienceAsync);

        JsonShape.AssertMatches(toOwner, AudienceEventSample("pollControl"), "audience-events.json pollControl", ControlFrame);
        toOwner.GetProperty("control").GetProperty("round").GetString().ShouldBe(round);
        toOwner.GetProperty("control").GetProperty("settings").GetProperty("correct").GetInt32().ShouldBe(2);
        toOwner.GetRawText().ShouldNotContain(poll.DeckId, Case.Sensitive);
        // Neither guest's first poll frame is the control published before everything else: it carries counts and the
        // correct answer, and went to the presenter's watch only.
        guestFirst.GetProperty("type").GetString().ShouldBe("pollTally");
        guestFirst.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        watcherFirst.GetProperty("type").GetString().ShouldBe("pollTally");
        watcherFirst.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([1, 3, 0]);
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task WatchPoll_WhenGuestHasNotAnswered_ShouldRefuse(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        await using var guest = await GuestClient.ConnectAsync(Factory, transport);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);

        var refused = await Should.ThrowAsync<HubException>(() => guest.WatchPollAsync(poll.Code));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, round, 2, [1, 1, 0]));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Audience, round, 2, null));
        var first = await HubReceiver.NextPollEventAsync(guest.NextAudienceAsync);

        refused.Message.ShouldContain(DeckHub.NOT_ANSWERED, Case.Sensitive);
        first.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task WatchPoll_WhenNoGuestCookie_ShouldRefuseWithNoGuest()
    {
        var poll = await SeedPollAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);

        var refused = await Should.ThrowAsync<HubException>(() => guest.WatchPollAsync(poll.Code));

        refused.Message.ShouldContain(DeckHub.NO_GUEST, Case.Sensitive);
    }

    // Group membership ends with the connection: the same guest on a new connection gets no results until it watches again.
    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task WatchPoll_WhenGuestReconnects_ShouldNeedCallingAgain(HttpTransportType transport)
    {
        var poll = await SeedPollAsync();
        var round = Guid.NewGuid().ToString();
        var first = await GuestClient.ConnectAsync(Factory, transport);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        Answers.SetWatch(poll.Code, first.GuestId, round);
        await first.WatchPollAsync(poll.Code);
        await first.DisposeAsync();
        await using var again = await GuestClient.ConnectAsync(Factory, transport, guestId: first.GuestId);
        await again.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);

        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, round, 1, [1, 0, 0]));
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Audience, round, 1, null));
        var beforeWatching = await HubReceiver.NextPollEventAsync(again.NextAudienceAsync);
        await again.WatchPollAsync(poll.Code);
        await Bus.PublishAsync(new PollTally(poll.DeckId, PollTallyTarget.Results, round, 2, [1, 1, 0]));
        var afterWatching = await HubReceiver.NextPollEventAsync(again.NextAudienceAsync);

        beforeWatching.GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        afterWatching.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([1, 1, 0]);
    }

    private static readonly JsonShapeOptions ControlFrame = new()
    {
        NullablePaths = ["control.round", "control.settings.max", "control.settings.correct"],
    };

    private async Task<HubReceiver> WatchAsync(string deckId, HttpTransportType transport)
    {
        var owner = await HubReceiver.StartAsync(Factory, SignedInAs, transport);
        await owner.Connection.InvokeAsync("WatchAudience", deckId);
        return owner;
    }

    private static PollActivity ActivityOf(string round)
        => new(round, 1, PollPhase.Open, PollSeed.QUESTION, PollSeed.OPTIONS, PollPick.UpTo, 2, PollResults.AfterAnswer, false, null);

    private static PollControlView ControlOf(int slide, string round)
        => new(
            140,
            slide,
            round,
            1,
            PollPhase.Open,
            new PollSettings(PollPick.One, null, PollResults.AfterAnswer, 2, PollOpens.OnArrival, PollPrivacy.Private),
            PollSeed.QUESTION,
            PollSeed.OPTIONS,
            4,
            10,
            [1, 3, 0],
            false,
            false,
            false,
            []);

    private static JsonElement PollActivitySample(string name) => SampleOf("poll-activity.json", name);

    private static JsonElement AudienceEventSample(string name) => SampleOf("audience-events.json", name);

    private static JsonElement SampleOf(string file, string name)
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));
        return contract.RootElement.GetProperty(name).Clone();
    }
}
