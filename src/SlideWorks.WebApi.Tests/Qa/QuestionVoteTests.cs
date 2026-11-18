using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// PUT /api/join/{code}/questions/votes on the real IGuestQuestions (Q4, D20, D21): what is stored from a guest's picks, the
// revision, the 409 with the guest's current view, the picks' rules, the room's round and the 30 vote changes a minute.
// Questions and earlier upvotes come from QaRowSeed on QaSeed's live deck; the room is FakeAudienceRoom's, pointed at the
// seeded round; the answer writer and the tally are the app's own, so a vote is read back through GET. The app is shared by
// the fixture, so every test seeds a deck of its own, and each starts a new minute for the join-code and vote limits.
[TestFixture]
[CancelAfter(60000)]
public class QuestionVoteTests : QaTestFixture
{
    private const string FIRST = "How do you split a big change?";

    private const string SECOND = "What is the smallest slice you have shipped?";

    private FakeAudienceRoom Rooms => Factory.Services.GetRequiredService<FakeAudienceRoom>();

    protected override void ConfigureQaServices(IServiceCollection services) => FakeAudienceRoom.AddTo(services);

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task VoteAsync_WhenPickAdded_ShouldStoreItAndRaiseTheRevision()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [2], 0));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Picks(body).ShouldBe([2]);
        body.GetProperty("revision").GetInt32().ShouldBe(1);
        Picks(view.GetProperty("mine")).ShouldBe([2]);
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(1);
        Votes(view).ShouldBe([(1, 0), (2, 1)]);
    }

    [Test]
    public async Task VoteAsync_WhenPickRemoved_ShouldStoreTheRestAndRaiseTheRevision()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 1, 2);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 1));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Picks(body).ShouldBe([1]);
        body.GetProperty("revision").GetInt32().ShouldBe(2);
        Votes(view).ShouldBe([(1, 1), (2, 0)]);
    }

    [Test]
    public async Task VoteAsync_WhenPickIsOnAnAnsweredQuestion_ShouldKeepTheOldPick()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Answered);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 2);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1, 2], 1));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Picks(body).ShouldBe([2]);
        body.GetProperty("revision").GetInt32().ShouldBe(2);
        Votes(view).ShouldBe([(1, 0), (2, 1)]);
    }

    [Test]
    public async Task VoteAsync_WhenRemovingAVoteOnAnAnsweredQuestion_ShouldKeepIt()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Answered);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 1);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [2], 1));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Picks(body).ShouldBe([1, 2]);
        Votes(view).ShouldBe([(1, 1), (2, 1)]);
    }

    [TestCase(false, QuestionState.Waiting)]
    [TestCase(true, QuestionState.Open)]
    public async Task VoteAsync_WhenQuestionCannotTakeAVote_ShouldKeepItsPicksAsTheyWere(bool hidden, QuestionState state)
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, state, hidden);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND, state, hidden);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who reviews the slices?");
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 1);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [2, 3], 1));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Picks(body).ShouldBe([1, 3]);
    }

    [Test]
    public async Task VoteAsync_WhenRevisionOld_ShouldBeStaleWithTheCurrentView()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 1);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [2], 0));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(GuestQaErrors.STALE);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("stale"), "question-votes.json stale", "current.room.round", "current.questions[].name");
        Picks(body.GetProperty("current").GetProperty("mine")).ShouldBe([1]);
        body.GetProperty("current").GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(1);
        Votes(body.GetProperty("current")).ShouldBe([(1, 1), (2, 0)]);
        Votes(view).ShouldBe([(1, 1), (2, 0)]);
    }

    [Test]
    public async Task VoteAsync_WhenRoundIsNotTheRoomsRound_ShouldBeStale()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(Guid.NewGuid().ToString(), [1], 0));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(GuestQaErrors.STALE);
        body.GetProperty("current").GetProperty("room").GetProperty("round").GetString().ShouldBe(qa.RoundId.ToString());
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task VoteAsync_WhenTheWriterFindsTheRoundClosed_ShouldBeStaleWithTheCurrentView()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);
        await Rows.CloseAsync(qa.RoundId, joinedAtClose: 1);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(GuestQaErrors.STALE);
        body.GetProperty("current").GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
        Picks(body.GetProperty("current").GetProperty("mine")).ShouldBeEmpty();
    }

    [TestCase(7)]
    [TestCase(0)]
    [TestCase(-1)]
    public async Task VoteAsync_WhenNumberUnknown_ShouldNameIt(int number)
    {
        var (response, body, view) = await RefusedVoteAsync([1, number]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe($"picks: unknown question {number}");
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task VoteAsync_WhenNumberRepeated_ShouldBeDuplicate()
    {
        var (response, body, view) = await RefusedVoteAsync([1, 2, 1]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(VoteRules.DUPLICATE);
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task VoteAsync_WhenStaleAndUnknown_ShouldBeStaleFirst()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [9], 4));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestQaErrors.STALE);
    }

    [TestCase(QaRoomQuestions.Closed)]
    [TestCase(QaRoomQuestions.OffHere)]
    public async Task VoteAsync_WhenQuestionsClosed_ShouldStillCount(QaRoomQuestions questions)
    {
        var qa = await LiveAsync(questions);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 0));
        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Votes(view).ShouldBe([(1, 1)]);
    }

    [Test]
    public async Task VoteAsync_WhenQuestionsOff_ShouldBeRefused()
    {
        var qa = await SeedQaAsync();
        Rooms.Set(qa.DeckId, QaRoomRules.For(QaSettings.Default with { Questions = QaQuestions.Off }, QaSlideSwitch.On, new QaRoundState(qa.RoundId.ToString(), 3)));
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);

        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestQaErrors.QUESTIONS_OFF);
    }

    [Test]
    public async Task VoteAsync_WhenThirtyFirstInAMinute_ShouldBeLimited()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        for (var revision = 0; revision < VoteLimiter.PER_MINUTE; revision++)
        {
            (await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [(revision % 2) + 1], revision))).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var limited = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], VoteLimiter.PER_MINUTE));
        var body = await BodyAsync(limited);
        Clock.Advance(TimeSpan.FromMinutes(1));
        var later = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], VoteLimiter.PER_MINUTE));

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        body.GetProperty("error").GetString().ShouldBe(GuestQaErrors.TOO_MANY_VOTES);
        later.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task VoteAsync_WhenGuestIsNotOnTheRoster_ShouldBeNoSession()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var stranger = await GuestClient.ConnectAsync(Factory);

        var response = await stranger.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 0));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    // A vote the picks' rules refuse, on a live deck of the test's own with two open questions, and the guest's view after.
    private async Task<(HttpResponseMessage Response, JsonElement Body, JsonElement View)> RefusedVoteAsync(int[] picks)
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);
        await using var guest = await JoinAsync(qa);
        var response = await guest.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), picks, 0));
        return (response, await BodyAsync(response), await BodyAsync(await guest.GetQuestionsAsync(qa.Code)));
    }

    // QaSeed's live deck with the room pointing at its round, questions as given.
    private async Task<SeededQa> LiveAsync(QaRoomQuestions questions = QaRoomQuestions.Open)
    {
        var qa = await SeedQaAsync();
        Rooms.Set(qa.DeckId, new QaRoom(qa.RoundId.ToString(), questions, true, true, 3));
        return qa;
    }

    private async Task<GuestClient> JoinAsync(SeededQa qa)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);
        return guest;
    }

    private static int[] Picks(JsonElement mine) => [.. mine.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32())];

    private static (int Number, int Votes)[] Votes(JsonElement view)
        => [.. view.GetProperty("questions").EnumerateArray().Select(q => (q.GetProperty("number").GetInt32(), q.GetProperty("votes").GetInt32()))];

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement Example(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));
        return document.RootElement.Clone();
    }
}
