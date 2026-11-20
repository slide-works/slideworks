using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The presenter's view of a deck's Q&A (Q6) through GET /api/decks/{id}/qa and the real IQuestionModeration.ControlAsync:
// the questions of the open round with their votes from the tally, the counts, here and the switched-off slides, the
// settings and the reaction totals, live and not. Questions, votes and switches come from QaRowSeed on QaSeed's live deck;
// the settings and the reaction totals are the settable fakes. The tally is the real one behind HookedTallies, which can run
// a test's step once inside the view's tally read, that is in the middle of the view's reads. The app, and so the fakes, is
// shared by the fixture: each test makes its own deck.
[TestFixture]
public class QaControlViewTests : QaTestFixture
{
    private FakeQaSettings Settings => Factory.Services.GetRequiredService<FakeQaSettings>();

    private FakeReactions Reactions => Factory.Services.GetRequiredService<FakeReactions>();

    private HookedTallies Tallies => Factory.Services.GetRequiredService<HookedTallies>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakeQaSettings.AddTo(services);
        FakeReactions.AddTo(services);
        services.AddSingleton<PollTallies>();
        services.AddSingleton(provider => new HookedTallies(provider.GetRequiredService<PollTallies>()));
        services.AddSingleton<IPollTallies>(provider => provider.GetRequiredService<HookedTallies>());
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
    public async Task ControlAsync_WhenQuestionsOff_ShouldListNothing()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?", onScreen: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who reviews the slices?", QuestionState.Waiting);
        Settings.Set(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });

        var view = await ViewAsync(qa.DeckId);

        view.GetProperty("live").GetBoolean().ShouldBeTrue();
        view.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("onScreen").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("questions").GetArrayLength().ShouldBe(0);
        view.GetProperty("counts").EnumerateObject().ShouldAllBe(count => count.Value.GetInt32() == 0);
        view.GetProperty("settings").GetProperty("questions").GetString().ShouldBe("off");
    }

    [TestCase(QaQuestions.Open)]
    [TestCase(QaQuestions.Closed)]
    public async Task ControlAsync_WhenQuestionsComeBackFromOff_ShouldListTheSameQuestionsAgain(QaQuestions back)
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?", onScreen: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who reviews the slices?", QuestionState.Waiting);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Do feature flags count?", QuestionState.Answered);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-a", 1, 3);
        var before = await ViewAsync(qa.DeckId);
        Settings.Set(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });
        (await ViewAsync(qa.DeckId)).GetProperty("questions").GetArrayLength().ShouldBe(0);
        Settings.Set(qa.DeckId, QaSettings.Default with { Questions = back });

        var view = await ViewAsync(qa.DeckId);

        view.GetProperty("round").GetString().ShouldBe(qa.RoundId.ToString());
        view.GetProperty("onScreen").GetInt32().ShouldBe(1);
        view.GetProperty("questions").GetRawText().ShouldBe(before.GetProperty("questions").GetRawText());
        view.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("votes").GetInt32()).ShouldBe([1, 0, 1]);
        view.GetProperty("counts").GetRawText().ShouldBe(before.GetProperty("counts").GetRawText());
    }

    [Test]
    public async Task ControlAsync_WhenAQuestionIsAskedDuringTheRead_ShouldStillAnswer()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?");
        Tallies.OnceInsideRead(qa.RoundId.ToString(), () => QaRowSeed.QuestionAsync(qa.RoundId, "Asked while the view was read"));

        var response = await Client.GetAsync($"/api/decks/{qa.DeckId}/qa");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Tallies.Pending(qa.RoundId.ToString()).ShouldBeFalse();
        (await ViewAsync(qa.DeckId)).GetProperty("questions").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task ControlAsync_WhenRead_ShouldTakeItsSeqBeforeReadingTheRows()
    {
        var qa = await SeedQaAsync();
        var seq = Factory.Services.GetRequiredService<QaSeq>();
        long during = 0;
        Tallies.OnceInsideRead(qa.RoundId.ToString(), () =>
        {
            during = seq.Next();
            return Task.CompletedTask;
        });

        var view = await ViewAsync(qa.DeckId);

        during.ShouldBeGreaterThan(0);
        view.GetProperty("seq").GetInt64().ShouldBeLessThan(during);
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

    // The real tally with a hook: a test's step runs once at the start of the next ReadAsync of its round, before the read.
    private sealed class HookedTallies(PollTallies inner) : IPollTallies
    {
        private readonly ConcurrentDictionary<string, Func<Task>> _hooks = new(StringComparer.Ordinal);

        public void OnceInsideRead(string roundId, Func<Task> step) => _hooks[roundId] = step;

        // Whether the round's step is still waiting for a read.
        public bool Pending(string roundId) => _hooks.ContainsKey(roundId);

        public Task ApplyAsync(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks, bool hadText = false, bool hasText = false) => inner.ApplyAsync(round, oldPicks, newPicks, hadText, hasText);

        public async Task<PollCounts> ReadAsync(string roundId, int optionCount)
        {
            if (_hooks.TryRemove(roundId, out var step))
            {
                await step();
            }
            return await inner.ReadAsync(roundId, optionCount);
        }

        public IDisposable Changed(Func<PollRoundRef, Task> handler) => inner.Changed(handler);
    }

    private static JsonElement Example()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "qa-control.json")));
        return document.RootElement.Clone();
    }
}
