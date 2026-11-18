using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The presenter's view of a deck's Q&A (Q6) through GET /api/decks/{id}/qa and the real IQuestionModeration.ControlAsync:
// the questions of the open round with their votes from the tally, the counts, here and the switched-off slides, the
// settings and the reaction totals, live and not. Questions, votes and switches come from QaRowSeed on QaSeed's live deck;
// the settings and the reaction totals are the settable fakes. The app, and so the fakes, is shared by the fixture: each
// test makes its own deck.
[TestFixture]
public class QaControlViewTests : QaTestFixture
{
    private FakeQaSettings Settings => Factory.Services.GetRequiredService<FakeQaSettings>();

    private FakeReactions Reactions => Factory.Services.GetRequiredService<FakeReactions>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakeQaSettings.AddTo(services);
        FakeReactions.AddTo(services);
    }

    [Test]
    public async Task ControlAsync_WhenLive_ShouldCountEachState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "First open");
        await QaRowSeed.QuestionAsync(qa.RoundId, "Waiting", QuestionState.Waiting);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Answered", QuestionState.Answered);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Hidden open", hidden: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Hidden waiting", QuestionState.Waiting, hidden: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Second open");

        var view = await ViewAsync(qa.DeckId);

        JsonShape.AssertMatches(view, Example().GetProperty("live"), "qa-control.json live", ScreenShapes.QaControl);
        view.GetProperty("live").GetBoolean().ShouldBeTrue();
        var counts = view.GetProperty("counts");
        counts.GetProperty("open").GetInt32().ShouldBe(2);
        counts.GetProperty("waiting").GetInt32().ShouldBe(1);
        counts.GetProperty("answered").GetInt32().ShouldBe(1);
        counts.GetProperty("hidden").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task ControlAsync_WhenLive_ShouldListEveryQuestionInNumberOrderWithItsVotes()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?", name: "Priya");
        await QaRowSeed.QuestionAsync(qa.RoundId, "What is the smallest slice?", onScreen: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who reviews the slices?", QuestionState.Waiting);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-a", 1, 2);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-b", 2);

        var view = await ViewAsync(qa.DeckId);

        view.GetProperty("round").GetString().ShouldBe(qa.RoundId.ToString());
        view.GetProperty("onScreen").GetInt32().ShouldBe(2);
        var questions = view.GetProperty("questions").EnumerateArray().ToList();
        questions.Select(q => q.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3]);
        questions.Select(q => q.GetProperty("text").GetString()).ShouldBe(["How do you split a big change?", "What is the smallest slice?", "Who reviews the slices?"]);
        questions.Select(q => q.GetProperty("votes").GetInt32()).ShouldBe([1, 2, 0]);
        questions.Select(q => q.GetProperty("state").GetString()).ShouldBe(["open", "open", "waiting"]);
        questions[0].GetProperty("name").GetString().ShouldBe("Priya");
        questions[1].GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        questions[1].GetProperty("onScreen").GetBoolean().ShouldBeTrue();
        questions.ShouldAllBe(q => q.GetProperty("age").GetString() == "now");
    }

    [Test]
    public async Task ControlAsync_WhenLive_ShouldTakeHereFromThePresentersSlideAndItsSwitch()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QaSlideId, new QaSlideSwitch(false, true));
        Presentations.Set(SettablePresentations.At(qa.DeckId, qa.QaSlide));

        var view = await ViewAsync(qa.DeckId);

        var here = view.GetProperty("here");
        here.GetProperty("slide").GetInt32().ShouldBe(qa.QaSlide);
        here.GetProperty("questions").GetBoolean().ShouldBeFalse();
        here.GetProperty("reactions").GetBoolean().ShouldBeTrue();
        var slide = view.GetProperty("slides").EnumerateArray().ShouldHaveSingleItem();
        slide.GetProperty("slide").GetInt32().ShouldBe(qa.QaSlide);
    }

    [Test]
    public async Task ControlAsync_WhenLive_ShouldCarryTheSessionsReactionTotals()
    {
        var qa = await SeedQaAsync();
        Reactions.SetTotals(qa.SessionId, new ReactionCounts(41, 12, 7, 3, 28));

        var view = await ViewAsync(qa.DeckId);

        var reactions = view.GetProperty("reactions");
        reactions.GetProperty("thumbsUp").GetInt32().ShouldBe(41);
        reactions.GetProperty("heart").GetInt32().ShouldBe(12);
        reactions.GetProperty("laugh").GetInt32().ShouldBe(7);
        reactions.GetProperty("wow").GetInt32().ShouldBe(3);
        reactions.GetProperty("clap").GetInt32().ShouldBe(28);
    }

    [Test]
    public async Task ControlAsync_WhenQuestionsOff_ShouldHaveNoRound()
    {
        var qa = await SeedQaAsync();
        Settings.Set(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });

        var view = await ViewAsync(qa.DeckId);

        view.GetProperty("live").GetBoolean().ShouldBeTrue();
        view.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("settings").GetProperty("questions").GetString().ShouldBe("off");
    }

    [Test]
    public async Task ControlAsync_WhenNotLive_ShouldHaveNoHereAndStillListSwitchedOffSlides()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?");
        await QaRowSeed.SwitchAsync(qa.QaSlideId, new QaSlideSwitch(false, true));
        Settings.Set(qa.DeckId, QaSettings.Default with { Review = true });
        Reactions.SetTotals(qa.SessionId, new ReactionCounts(1, 1, 1, 1, 1));
        (await Client.DeleteAsync($"/api/decks/{qa.DeckId}/live")).EnsureSuccessStatusCode();

        var view = await ViewAsync(qa.DeckId);

        JsonShape.AssertMatches(view, Example().GetProperty("live"), "qa-control.json live", ScreenShapes.QaControl);
        view.GetProperty("live").GetBoolean().ShouldBeFalse();
        view.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("here").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("onScreen").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("questions").GetArrayLength().ShouldBe(0);
        view.GetProperty("counts").EnumerateObject().ShouldAllBe(count => count.Value.GetInt32() == 0);
        view.GetProperty("reactions").EnumerateObject().ShouldAllBe(count => count.Value.GetInt32() == 0);
        view.GetProperty("settings").GetProperty("review").GetBoolean().ShouldBeTrue();
        var slide = view.GetProperty("slides").EnumerateArray().ShouldHaveSingleItem();
        slide.GetProperty("slide").GetInt32().ShouldBe(qa.QaSlide);
        slide.GetProperty("questions").GetBoolean().ShouldBeFalse();
        slide.GetProperty("reactions").GetBoolean().ShouldBeTrue();
    }

    private async Task<JsonElement> ViewAsync(string deckId)
    {
        var response = await Client.GetAsync($"/api/decks/{deckId}/qa");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement Example()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "qa-control.json")));
        return document.RootElement.Clone();
    }
}
