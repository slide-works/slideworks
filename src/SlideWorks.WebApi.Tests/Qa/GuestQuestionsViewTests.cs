using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// GET /api/join/{code}/questions on the real IGuestQuestions (Q4): which questions a guest sees (every open and answered one
// that isn't hidden, plus its own in any state, hidden winning over the state under it), the votes from the tally, its own
// upvotes, the age labels, the room and the roster. Questions and upvotes come from QaRowSeed on QaSeed's live deck; the room
// is FakeAudienceRoom's. The app is shared by the fixture, so every test seeds a deck of its own, and each starts a new
// minute for the join-code limit.
[TestFixture]
[CancelAfter(60000)]
public class GuestQuestionsViewTests : QaTestFixture
{
    private const string FIRST = "How do you split a big change?";

    private const string SECOND = "What is the smallest slice you have shipped?";

    private FakeAudienceRoom Rooms => Factory.Services.GetRequiredService<FakeAudienceRoom>();

    protected override void ConfigureQaServices(IServiceCollection services) => FakeAudienceRoom.AddTo(services);

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task ViewAsync_WhenQuestionHidden_ShouldShowItOnlyToItsAsker()
    {
        var qa = await LiveAsync();
        await using var asker = await JoinAsync(qa);
        await using var other = await JoinAsync(qa);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND, hidden: true, guestId: asker.GuestId, name: "Priya");

        var mine = await BodyAsync(await asker.GetQuestionsAsync(qa.Code));
        var theirs = await BodyAsync(await other.GetQuestionsAsync(qa.Code));

        Numbers(mine).ShouldBe([1, 2]);
        var own = Question(mine, 2);
        own.GetProperty("state").GetString().ShouldBe("hidden");
        own.GetProperty("yours").GetBoolean().ShouldBeTrue();
        own.GetProperty("name").GetString().ShouldBe("Priya");
        Question(mine, 1).GetProperty("yours").GetBoolean().ShouldBeFalse();
        Numbers(theirs).ShouldBe([1]);
    }

    [Test]
    public async Task ViewAsync_WhenQuestionWaiting_ShouldShowItOnlyToItsAsker()
    {
        var qa = await LiveAsync();
        await using var asker = await JoinAsync(qa);
        await using var other = await JoinAsync(qa);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting, guestId: asker.GuestId);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND, QuestionState.Answered);

        var mine = await BodyAsync(await asker.GetQuestionsAsync(qa.Code));
        var theirs = await BodyAsync(await other.GetQuestionsAsync(qa.Code));

        Numbers(mine).ShouldBe([1, 2]);
        Question(mine, 1).GetProperty("state").GetString().ShouldBe("waiting");
        Question(mine, 1).GetProperty("yours").GetBoolean().ShouldBeTrue();
        Numbers(theirs).ShouldBe([2]);
        Question(theirs, 2).GetProperty("state").GetString().ShouldBe("answered");
    }

    [Test]
    public async Task ViewAsync_WhenOwnQuestionHiddenWhileWaiting_ShouldBeHidden()
    {
        var qa = await LiveAsync();
        await using var asker = await JoinAsync(qa);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting, hidden: true, guestId: asker.GuestId);

        var mine = await BodyAsync(await asker.GetQuestionsAsync(qa.Code));

        Question(mine, 1).GetProperty("state").GetString().ShouldBe("hidden");
    }

    [Test]
    public async Task ViewAsync_WhenVotesStored_ShouldCountThemAndGiveTheGuestItsOwn()
    {
        var qa = await LiveAsync();
        await using var guest = await JoinAsync(qa);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, name: "Sam K");
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND, onScreen: true);
        await QaRowSeed.VoteAsync(qa.RoundId, guest.GuestId, 1, 2);
        await QaRowSeed.VoteAsync(qa.RoundId, GuestCookie.NewId(), 2);
        Clock.Advance(TimeSpan.FromMinutes(3));

        var response = await guest.GetQuestionsAsync(qa.Code);
        var view = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(view, Example("guest-questions.json").GetProperty("view"), "guest-questions.json view", ScreenShapes.GuestQuestions);
        view.GetProperty("room").GetProperty("round").GetString().ShouldBe(qa.RoundId.ToString());
        view.GetProperty("room").GetProperty("listRevision").GetInt64().ShouldBe(3);
        Question(view, 1).GetProperty("votes").GetInt32().ShouldBe(1);
        Question(view, 2).GetProperty("votes").GetInt32().ShouldBe(2);
        Question(view, 1).GetProperty("text").GetString().ShouldBe(FIRST);
        Question(view, 1).GetProperty("name").GetString().ShouldBe("Sam K");
        Question(view, 1).GetProperty("age").GetString().ShouldBe("3 min");
        Question(view, 1).GetProperty("onScreen").GetBoolean().ShouldBeFalse();
        Question(view, 2).GetProperty("onScreen").GetBoolean().ShouldBeTrue();
        Question(view, 2).GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("mine").GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1, 2]);
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task ViewAsync_WhenQuestionsOff_ShouldHaveTheRoomAndNoQuestions()
    {
        var qa = await SeedQaAsync();
        var room = QaRoomRules.For(QaSettings.Default with { Questions = QaQuestions.Off }, QaSlideSwitch.On, new QaRoundState(qa.RoundId.ToString(), 3));
        Rooms.Set(qa.DeckId, room);
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var guest = await JoinAsync(qa);

        var view = await BodyAsync(await guest.GetQuestionsAsync(qa.Code));

        view.GetProperty("room").GetProperty("questions").GetString().ShouldBe("off");
        view.GetProperty("room").GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("questions").GetArrayLength().ShouldBe(0);
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task ViewAsync_WhenGuestIsNotOnTheRoster_ShouldBeNoSession()
    {
        var qa = await LiveAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        await using var stranger = await GuestClient.ConnectAsync(Factory);

        var response = await stranger.GetQuestionsAsync(qa.Code);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task ViewAsync_WhenDeckHasNoRoom_ShouldBeNoSession()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa);
        Rooms.Set(qa.DeckId, null);

        var response = await guest.GetQuestionsAsync(qa.Code);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // QaSeed's live deck with the room pointing at its round.
    private async Task<SeededQa> LiveAsync()
    {
        var qa = await SeedQaAsync();
        Rooms.Set(qa.DeckId, new QaRoom(qa.RoundId.ToString(), QaRoomQuestions.Open, true, true, 3));
        return qa;
    }

    private async Task<GuestClient> JoinAsync(SeededQa qa)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);
        return guest;
    }

    private static int[] Numbers(JsonElement view) => [.. view.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("number").GetInt32())];

    private static JsonElement Question(JsonElement view, int number)
        => view.GetProperty("questions").EnumerateArray().Single(q => q.GetProperty("number").GetInt32() == number);

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
