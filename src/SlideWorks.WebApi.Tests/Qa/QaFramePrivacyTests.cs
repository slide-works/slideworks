using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// "Hidden means hidden" on the wire (Q4, Q5): the raw frames a guest's connection and a presenter's watch connection receive
// over the real hub, through the real publisher and routing. FakeQuestionModeration gives the presenter's view of the deck;
// the deck is live with an open Q&A round (QaSeed). Each test publishes once, then puts an audienceEnded marker on the bus:
// the relay forwards in order, so every frame the guest got from the publish arrives before the marker, and all of them are
// checked as raw JSON. The watch connection's frames are the positive control: the presenter gets what the guest must not.
// The app is shared by the fixture, so each test seeds its own deck, code and round.
[TestFixture]
[CancelAfter(60000)]
public class QaFramePrivacyTests : QaTestFixture
{
    private const string SECRET_TEXT = "Is the reorg announced next week?";
    private const string SECRET_NAME = "Marguerite";

    private readonly List<IAsyncDisposable> _connections = [];

    private FakeQuestionModeration Moderation => Factory.Services.GetRequiredService<FakeQuestionModeration>();

    protected override void ConfigureQaServices(IServiceCollection services) => FakeQuestionModeration.AddTo(services);

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    [Test]
    public async Task Frames_WhenAQuestionIsHidden_ShouldNotCarryItToAGuest()
    {
        var room = await RoomAsync([
            Question(1, "What do you ship first?", QuestionState.Open, 4),
            Question(2, SECRET_TEXT, QuestionState.Open, 7) with { Name = SECRET_NAME, Hidden = true },
            Question(3, "How small is small?", QuestionState.Answered, 2)]);

        var guest = await PublishAndReadGuestAsync(room);
        var control = await HubReceiver.NextQaEventAsync(room.Watcher.NextAudienceAsync);

        TallyNumbers(guest).ShouldBe([1, 3]);
        guest.ShouldAllBe(frame => !frame.GetRawText().Contains(SECRET_TEXT) && !frame.GetRawText().Contains(SECRET_NAME));
        control.GetProperty("type").GetString().ShouldBe("questionControl");
        control.GetRawText().ShouldContain(SECRET_TEXT);
        control.GetRawText().ShouldContain(SECRET_NAME);
    }

    [Test]
    public async Task Frames_WhenAQuestionIsWaiting_ShouldNotCarryItsVotesToAGuest()
    {
        var room = await RoomAsync([
            Question(1, SECRET_TEXT, QuestionState.Waiting, 9),
            Question(2, "What do you ship first?", QuestionState.Open, 5)]);

        var guest = await PublishAndReadGuestAsync(room);
        var control = await HubReceiver.NextQaEventAsync(room.Watcher.NextAudienceAsync);

        var tally = guest.Where(frame => TypeOf(frame) == "questionTally").ShouldHaveSingleItem();
        tally.GetProperty("votes").EnumerateArray().Select(v => (v.GetProperty("number").GetInt32(), v.GetProperty("votes").GetInt32())).ShouldBe([(2, 5)]);
        guest.ShouldAllBe(frame => !frame.GetRawText().Contains(SECRET_TEXT));
        control.GetProperty("control").GetProperty("questions").EnumerateArray().First().GetProperty("votes").GetInt32().ShouldBe(9);
    }

    // The guest gets the round's questionTally; the presenter's questionControl and the reactions burst go only to the watch.
    [Test]
    public async Task Frames_WhenGuest_ShouldNeverCarryQuestionControlOrReactions()
    {
        var room = await RoomAsync([Question(1, "What do you ship first?", QuestionState.Open, 3)]);
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();

        var guest = await PublishAndReadGuestAsync(room, () => bus.PublishAsync(new ReactionsBurst(room.Qa.DeckId, room.Qa.QuoteSlide, new ReactionCounts(2, 1, 0, 0, 1))));
        var watched = new[]
        {
            await HubReceiver.NextQaEventAsync(room.Watcher.NextAudienceAsync),
            await HubReceiver.NextQaEventAsync(room.Watcher.NextAudienceAsync),
        };

        guest.Select(TypeOf).ShouldContain("questionTally");
        guest.Select(TypeOf).ShouldNotContain("questionControl");
        guest.Select(TypeOf).ShouldNotContain("reactions");
        watched.Select(TypeOf).ShouldBe(["questionControl", "reactions"]);
    }

    // A live deck with an open Q&A round, the presenter's view set to these questions, the presenter watching and one guest
    // joined.
    private async Task<Room> RoomAsync(IReadOnlyList<QaQuestion> questions)
    {
        var qa = await SeedQaAsync();
        Moderation.Set(qa.DeckId, Live(qa, questions));
        var watcher = await HubReceiver.StartAsync(Factory, SignedInAs);
        _connections.Add(watcher);
        await watcher.Connection.InvokeAsync("WatchAudience", qa.DeckId);
        var guest = await GuestClient.ConnectAsync(Factory);
        _connections.Add(guest);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);
        return new Room(qa, watcher, guest);
    }

    // Publishes the deck once through the real publisher, then anything more the test sends, then the marker, and returns
    // every audience frame the guest received before the marker.
    private async Task<IReadOnlyList<JsonElement>> PublishAndReadGuestAsync(Room room, Func<Task>? more = null)
    {
        await Factory.Services.GetRequiredService<IQuestionPublisher>().PublishAsync(room.Qa.DeckId);
        if (more != null)
        {
            await more();
        }
        await Factory.Services.GetRequiredService<IAudienceEvents>().PublishAsync(new AudienceEnded(room.Qa.DeckId));
        var frames = new List<JsonElement>();
        for (var frame = await room.Guest.NextAudienceAsync(); TypeOf(frame) != "audienceEnded"; frame = await room.Guest.NextAudienceAsync())
        {
            frames.Add(frame);
        }
        return frames;
    }

    private static string? TypeOf(JsonElement frame) => frame.GetProperty("type").GetString();

    // The question numbers in the guest's questionTally frames, in order.
    private static IEnumerable<int> TallyNumbers(IEnumerable<JsonElement> frames)
        => frames.Where(frame => TypeOf(frame) == "questionTally")
            .SelectMany(frame => frame.GetProperty("votes").EnumerateArray())
            .Select(v => v.GetProperty("number").GetInt32());

    private static QaQuestion Question(int number, string text, QuestionState state, int votes)
        => new(number, text, null, "now", state, false, false, votes);

    private static QaControlView Live(SeededQa qa, IReadOnlyList<QaQuestion> questions)
        => new(
            1,
            true,
            qa.RoundId.ToString(),
            QaSettings.Default,
            new QaSlideView(qa.QuoteSlide, true, true),
            [],
            new QaCounts(
                questions.Count(q => !q.Hidden && q.State == QuestionState.Open),
                questions.Count(q => !q.Hidden && q.State == QuestionState.Waiting),
                questions.Count(q => !q.Hidden && q.State == QuestionState.Answered),
                questions.Count(q => q.Hidden)),
            null,
            questions,
            ReactionCounts.Zero);

    private sealed record Room(SeededQa Qa, HubReceiver Watcher, GuestClient Guest);
}
