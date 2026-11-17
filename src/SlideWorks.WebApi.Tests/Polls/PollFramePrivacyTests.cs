using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// "Hidden means hidden" on the wire (V5, D4): the raw frames each connection receives over the real hub, through the real
// tally, publisher and routing. FakePolls gives the round's settings and phase, the round and its answers are real rows
// (PollRowSeed), the tally is changed with ApplyAsync as the answer writer does, and FakePollAnswers says which guest may
// watch the results group. Each test publishes once, then puts an audienceEnded marker on the bus: the relay forwards in
// order, so every frame a guest got from the publish arrives before the marker, and all of them are checked. The pump may
// also publish after an answer; its frames obey the same rule. The app is shared by the fixture, so each test seeds its
// own deck, code and round.
[TestFixture]
[CancelAfter(60000)]
public class PollFramePrivacyTests : PollTestFixture
{
    private static readonly int[] _expected = [1, 2, 0];

    private readonly List<Room> _rooms = [];

    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    private FakePollAnswers Answers => Factory.Services.GetRequiredService<FakePollAnswers>();

    private IPollTallies Tallies => Factory.Services.GetRequiredService<IPollTallies>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakePolls.AddTo(services);
        FakePollAnswers.AddTo(services);
    }

    [Test]
    public async Task Guest_WhenResultsAreAfterClose_ShouldNeverReceiveCountsInAFrameWhileOpen()
    {
        var room = await RoomAsync(PollResults.AfterClose);

        var (notAnswered, answered) = await PublishAndReadAsync(room);

        notAnswered.ShouldNotBeEmpty();
        notAnswered.ShouldAllBe(frame => !CarriesCounts(frame));
        answered.ShouldNotBeEmpty();
        answered.ShouldAllBe(frame => !CarriesCounts(frame));
        notAnswered.ShouldContain(frame => IsTally(frame, 3));
    }

    // A guest who has not answered, and one who has but has not called WatchPoll, get only null-count tallies; the guest
    // who answered and watches gets the counts, so the test would see them if they leaked.
    [Test]
    public async Task Guest_WhenResultsAreAfterAnswerAndNotWatching_ShouldNeverReceiveCountsInAFrame()
    {
        var room = await RoomAsync(PollResults.AfterAnswer);
        await using var watcher = await JoinAsync(room);
        await AnswerAsync(room, watcher, 1);
        Answers.SetWatch(room.Poll.Code, watcher.GuestId, room.Round);
        await watcher.WatchPollAsync(room.Poll.Code);

        var (notAnswered, answered) = await PublishAndReadAsync(room);
        var watching = await FramesUntilMarkerAsync(watcher);

        notAnswered.ShouldAllBe(frame => !CarriesCounts(frame));
        answered.ShouldAllBe(frame => !CarriesCounts(frame));
        notAnswered.ShouldContain(frame => IsTally(frame, 4));
        watching.ShouldContain(frame => CountsOf(frame).SequenceEqual(new[] { 1, 3, 0 }));
    }

    [TestCase(PollResults.AfterClose)]
    [TestCase(PollResults.AfterAnswer)]
    public async Task Guest_WhenRevealed_ShouldReceiveCounts(PollResults results)
    {
        var room = await RoomAsync(results, revealed: true);

        var (notAnswered, answered) = await PublishAndReadAsync(room);

        notAnswered.ShouldContain(frame => CountsOf(frame).SequenceEqual(_expected));
        answered.ShouldContain(frame => CountsOf(frame).SequenceEqual(_expected));
    }

    [TestCase(PollResults.Live)]
    [TestCase(PollResults.AfterClose)]
    [TestCase(PollResults.AfterAnswer)]
    public async Task Guest_WhenClosed_ShouldReceiveTheFinalCounts(PollResults results)
    {
        var room = await RoomAsync(results);
        await CloseAsync(room);

        var (notAnswered, answered) = await PublishAndReadAsync(room, applyAnswers: false);

        notAnswered.ShouldContain(frame => CountsOf(frame).SequenceEqual(_expected));
        answered.ShouldContain(frame => CountsOf(frame).SequenceEqual(_expected));
    }

    [Test]
    public async Task Guest_WhenResultsAreLive_ShouldReceiveCountsBeforeAnswering()
    {
        var room = await RoomAsync(PollResults.Live);

        var (notAnswered, _) = await PublishAndReadAsync(room);

        notAnswered.ShouldContain(frame => CountsOf(frame).SequenceEqual(_expected));
    }

    // The presenter's watch gets pollControl with the counts whatever the room sees; guests never get pollControl.
    [TestCase(PollResults.AfterClose)]
    [TestCase(PollResults.AfterAnswer)]
    public async Task Presenter_WhenResultsAreHidden_ShouldStillReceiveCounts(PollResults results)
    {
        var room = await RoomAsync(results);

        var (notAnswered, answered) = await PublishAndReadAsync(room);
        // The pump may have published the earlier answers first; the publish after all three is the one with answered 3.
        var controls = new List<JsonElement>();
        do
        {
            controls.Add(await HubReceiver.NextPollEventAsync(room.Presenter.NextAudienceAsync));
        }
        while (controls[^1].GetProperty("control").GetProperty("answered").GetInt32() < 3);
        var control = controls[^1];

        controls.ShouldAllBe(frame => frame.GetProperty("type").GetString() == "pollControl" && frame.GetProperty("control").GetProperty("counts").ValueKind == JsonValueKind.Array);
        control.GetProperty("control").GetProperty("roomSeesCounts").GetBoolean().ShouldBeFalse();
        control.GetProperty("control").GetProperty("answered").GetInt32().ShouldBe(3);
        control.GetProperty("control").GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe(_expected);
        notAnswered.Concat(answered).ShouldNotContain(frame => frame.GetProperty("type").GetString() == "pollControl");
    }

    // A live deck with the test's poll, an open round of real rows, its view in FakePolls, the presenter watching and two
    // guests joined: one who will not answer and one who will.
    private async Task<Room> RoomAsync(PollResults results, bool revealed = false)
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, revealed: revealed);
        var id = round.Id.ToString();
        var settings = PollSettings.Default with { Results = results };
        Polls.Set(poll.DeckId, poll.Slide, ViewOf(poll.Slide, id, PollPhase.Open, settings, revealed, [0, 0, 0], 0));
        var presenter = await HubReceiver.StartAsync(Factory, SignedInAs);
        await presenter.Connection.InvokeAsync("WatchAudience", poll.DeckId);
        var room = new Room(poll, id, settings, revealed, presenter, null!, null!);
        room = room with { NotAnswered = await JoinAsync(room), Answered = await JoinAsync(room) };
        _rooms.Add(room);
        return room;
    }

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var room in _rooms)
        {
            await room.NotAnswered.DisposeAsync();
            await room.Answered.DisposeAsync();
            await room.Presenter.DisposeAsync();
        }
        _rooms.Clear();
    }

    private async Task<GuestClient> JoinAsync(Room room)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", room.Poll.Code);
        return guest;
    }

    // Writes the guest's answer row and applies it to the tally, as the answer writer does after its commit.
    private async Task AnswerAsync(Room room, GuestClient guest, params int[] picks)
    {
        await Rows.AnswerAsync(Guid.Parse(room.Round), guest.GuestId, picks);
        var roundRef = new PollRoundRef(room.Poll.DeckId, room.Poll.Slide, room.Poll.SlideId, room.Poll.SessionId.ToString(), room.Round, 3);
        await Tallies.ApplyAsync(roundRef, [], picks);
    }

    // Closes the round with its Final counted from the rows, and gives FakePolls the closed view, as PollReader would.
    private async Task CloseAsync(Room room)
    {
        await LoadAndAnswerAsync(room);
        var closed = await Rows.CloseAsync(Guid.Parse(room.Round), 3);
        var final = closed.Final!;
        Polls.Set(room.Poll.DeckId, room.Poll.Slide, ViewOf(room.Poll.Slide, room.Round, PollPhase.Closed, room.Settings, room.Revealed, final.Counts, final.Answered));
    }

    // Loads the round (empty), then the answering guest picks option 2 and two more guests, who are not connected, pick
    // options 1 and 2: three answered, counts [1, 2, 0].
    private async Task LoadAndAnswerAsync(Room room)
    {
        await Tallies.ReadAsync(room.Round, 3);
        await AnswerAsync(room, room.Answered, 1);
        await using var second = await GuestClient.ConnectAsync(Factory);
        await AnswerAsync(room, second, 0);
        await using var third = await GuestClient.ConnectAsync(Factory);
        await AnswerAsync(room, third, 1);
    }

    // Answers (unless the round is already closed with them), publishes the poll once through the real publisher, puts the
    // marker on the bus, and returns every frame each of the room's two guests received before it.
    private async Task<(IReadOnlyList<JsonElement> NotAnswered, IReadOnlyList<JsonElement> Answered)> PublishAndReadAsync(Room room, bool applyAnswers = true)
    {
        if (applyAnswers)
        {
            await LoadAndAnswerAsync(room);
        }
        await Factory.Services.GetRequiredService<IPollPublisher>().PublishAsync(room.Poll.DeckId, room.Poll.Slide);
        await Factory.Services.GetRequiredService<IAudienceEvents>().PublishAsync(new AudienceEnded(room.Poll.DeckId));
        return (await FramesUntilMarkerAsync(room.NotAnswered), await FramesUntilMarkerAsync(room.Answered));
    }

    // Every audience frame the guest received before the marker; each read waits at most 10 seconds.
    private static async Task<IReadOnlyList<JsonElement>> FramesUntilMarkerAsync(GuestClient guest)
    {
        var frames = new List<JsonElement>();
        for (var frame = await guest.NextAudienceAsync(); frame.GetProperty("type").GetString() != "audienceEnded"; frame = await guest.NextAudienceAsync())
        {
            frames.Add(frame);
        }
        return frames;
    }

    // True when the frame, at any depth, holds a "counts" array or a presenter's "control".
    private static bool CarriesCounts(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(p =>
            (p.NameEquals("counts") && p.Value.ValueKind == JsonValueKind.Array) || p.NameEquals("control") || CarriesCounts(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(CarriesCounts),
        _ => false,
    };

    private static bool IsTally(JsonElement frame, int answered)
        => frame.GetProperty("type").GetString() == "pollTally" && frame.GetProperty("answered").GetInt32() == answered;

    private static IEnumerable<int> CountsOf(JsonElement frame)
        => frame.GetProperty("type").GetString() == "pollTally" && frame.GetProperty("counts").ValueKind == JsonValueKind.Array
            ? frame.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32())
            : [];

    private static PollControlView ViewOf(int slide, string round, PollPhase phase, PollSettings settings, bool revealed, IReadOnlyList<int> counts, int answered)
        => new(
            1,
            slide,
            round,
            1,
            phase,
            settings,
            PollSeed.QUESTION,
            PollSeed.OPTIONS,
            answered,
            5,
            counts,
            PollVisibility.RoomSees(settings.Results, revealed, phase == PollPhase.Closed),
            revealed,
            false,
            []);

    private sealed record Room(SeededPoll Poll, string Round, PollSettings Settings, bool Revealed, HubReceiver Presenter, GuestClient NotAnswered, GuestClient Answered);
}
