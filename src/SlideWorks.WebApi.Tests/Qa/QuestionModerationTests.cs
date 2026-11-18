using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The presenter's seven actions on a question (Q6, D13) through the Q&A routes and the real IQuestionModeration: each
// action's state rule, that a refused action changes nothing (the view, the round's list revision, the activity, the
// publishes and the refreshes), and
// what an accepted one records and calls. Questions come from QaRowSeed on QaSeed's live deck; the publisher and the audience
// relay are the settable fakes, so a test counts their calls for its own deck. The app, and so the fakes, is shared by the
// fixture: each test makes its own deck.
[TestFixture]
public class QuestionModerationTests : QaTestFixture
{
    private const string FIRST = "How do you split a big change?";

    private const string SECOND = "What is the smallest slice you have shipped?";

    private FakeQuestionPublisher Publisher => Factory.Services.GetRequiredService<FakeQuestionPublisher>();

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    private QaRevisions Revisions => Factory.Services.GetRequiredService<QaRevisions>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakeQuestionPublisher.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    }

    [Test]
    public async Task ApproveAsync_WhenWaiting_ShouldOpenIt()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting);
        var revision = Revisions.Current(qa.RoundId.ToString());

        var view = await ActAsync(qa, "approve", 1);

        var question = view.GetProperty("questions").EnumerateArray().ShouldHaveSingleItem();
        question.GetProperty("state").GetString().ShouldBe("open");
        question.GetProperty("hidden").GetBoolean().ShouldBeFalse();
        view.GetProperty("counts").GetProperty("open").GetInt32().ShouldBe(1);
        view.GetProperty("counts").GetProperty("waiting").GetInt32().ShouldBe(0);
        Revisions.Current(qa.RoundId.ToString()).ShouldBeGreaterThan(revision);
        Publisher.PublishesOf(qa.DeckId).ShouldBe(1);
        Relay.Refreshed.Count(deck => deck == qa.DeckId).ShouldBe(1);
    }

    [Test]
    public async Task ApproveAsync_WhenOpen_ShouldBeWrongStateAndChangeNothing()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);

        var refused = await RefuseAsync(qa, "approve", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ApproveAsync_WhenHiddenWhileWaiting_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting, hidden: true);

        var refused = await RefuseAsync(qa, "approve", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ApproveAsync_WhenNumberUnknown_ShouldBeNotFound()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting);

        var refused = await RefuseAsync(qa, "approve", 2);

        refused.Status.ShouldBe(HttpStatusCode.NotFound);
        refused.Error.ShouldBe(QaRefusals.NO_QUESTION);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ApproveAsync_WhenLiveWithNoOpenRound_ShouldBeNotFound()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting);
        await Rows.CloseAsync(qa.RoundId, joinedAtClose: 0);

        var refused = await RefuseAsync(qa, "approve", 1);

        refused.Status.ShouldBe(HttpStatusCode.NotFound);
        refused.Error.ShouldBe(QaRefusals.NO_QUESTION);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ApproveAsync_WhenQuestionsOff_ShouldBeNotFoundAndChangeNothing()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting);
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });

        var refused = await RefuseAsync(qa, "approve", 1);

        refused.Status.ShouldBe(HttpStatusCode.NotFound);
        refused.Error.ShouldBe(QaRefusals.NO_QUESTION);
        refused.After.ShouldBe(refused.Before);
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default);
        (await ViewAsync(qa)).GetProperty("questions")[0].GetProperty("state").GetString().ShouldBe("waiting");
    }

    [Test]
    public async Task HideAsync_WhenOnScreen_ShouldTakeItOff()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, onScreen: true);

        var view = await ActAsync(qa, "hide", 1);

        var question = view.GetProperty("questions")[0];
        question.GetProperty("hidden").GetBoolean().ShouldBeTrue();
        question.GetProperty("onScreen").GetBoolean().ShouldBeFalse();
        question.GetProperty("state").GetString().ShouldBe("open");
        view.GetProperty("onScreen").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("counts").GetProperty("hidden").GetInt32().ShouldBe(1);
        view.GetProperty("counts").GetProperty("open").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task HideAsync_WhenAnswered_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Answered);

        var refused = await RefuseAsync(qa, "hide", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task RestoreAsync_WhenHiddenWhileWaiting_ShouldBeWaitingAgain()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting, hidden: true);

        var view = await ActAsync(qa, "restore", 1);

        var question = view.GetProperty("questions")[0];
        question.GetProperty("state").GetString().ShouldBe("waiting");
        question.GetProperty("hidden").GetBoolean().ShouldBeFalse();
        view.GetProperty("counts").GetProperty("waiting").GetInt32().ShouldBe(1);
        view.GetProperty("counts").GetProperty("hidden").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task RestoreAsync_WhenNotHidden_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);

        var refused = await RefuseAsync(qa, "restore", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task AnswerAsync_WhenOnScreen_ShouldTakeItOffAndAnswerIt()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, onScreen: true);

        var view = await ActAsync(qa, "answer", 1);

        var question = view.GetProperty("questions")[0];
        question.GetProperty("state").GetString().ShouldBe("answered");
        question.GetProperty("onScreen").GetBoolean().ShouldBeFalse();
        view.GetProperty("onScreen").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("counts").GetProperty("answered").GetInt32().ShouldBe(1);
    }

    [TestCase(QuestionState.Waiting, false)]
    [TestCase(QuestionState.Open, true)]
    public async Task AnswerAsync_WhenWaitingOrHidden_ShouldBeWrongState(QuestionState state, bool hidden)
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, state, hidden);

        var refused = await RefuseAsync(qa, "answer", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ReopenAsync_WhenAnswered_ShouldOpenIt()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Answered);

        var view = await ActAsync(qa, "reopen", 1);

        view.GetProperty("questions")[0].GetProperty("state").GetString().ShouldBe("open");
        view.GetProperty("counts").GetProperty("open").GetInt32().ShouldBe(1);
        view.GetProperty("counts").GetProperty("answered").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task ReopenAsync_WhenOpen_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);

        var refused = await RefuseAsync(qa, "reopen", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ShowAsync_WhenAnotherIsOnScreen_ShouldReplaceIt()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, onScreen: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECOND);

        var view = await ActAsync(qa, "show", 2);

        view.GetProperty("onScreen").GetInt32().ShouldBe(2);
        view.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("onScreen").GetBoolean()).ShouldBe([false, true]);
    }

    [Test]
    public async Task ShowAsync_WhenAlreadyOnScreen_ShouldChangeNothing()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, onScreen: true);
        var before = await SnapshotAsync(qa);

        var view = await ActAsync(qa, "show", 1);

        view.GetProperty("onScreen").GetInt32().ShouldBe(1);
        (await SnapshotAsync(qa)).ShouldBe(before);
    }

    [Test]
    public async Task ShowAsync_WhenHidden_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, hidden: true);

        var refused = await RefuseAsync(qa, "show", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task UnshowAsync_WhenNotOnScreen_ShouldBeWrongState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);

        var refused = await RefuseAsync(qa, "unshow", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.WRONG_STATE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task ShowAsync_WhenNotLive_ShouldBeNotLive()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST);
        (await Client.DeleteAsync($"/api/decks/{qa.DeckId}/live")).EnsureSuccessStatusCode();

        var refused = await RefuseAsync(qa, "show", 1);

        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.Error.ShouldBe(QaRefusals.NOT_LIVE);
        refused.After.ShouldBe(refused.Before);
    }

    [Test]
    public async Task Actions_WhenDone_ShouldLogTheActionWithoutTheText()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, FIRST, QuestionState.Waiting, name: "Priya");
        string[] actions = ["approve", "show", "unshow", "hide", "restore", "answer", "reopen"];

        foreach (var action in actions)
        {
            await ActAsync(qa, action, 1);
        }

        var summaries = (await DeckSeed.ActivityAsync(Client, qa.DeckId))
            .Select(entry => entry.GetProperty("summary").GetString()!)
            .Where(summary => summary.Contains("question", StringComparison.Ordinal))
            .Reverse()
            .ToList();
        summaries.ShouldBe(
        [
            "approved a question",
            "showed a question on screen",
            "took a question off screen",
            "hid a question",
            "restored a question",
            "answered a question",
            "reopened a question",
        ]);
        summaries.ShouldAllBe(summary => !summary.Contains("split", StringComparison.Ordinal) && !summary.Contains("Priya", StringComparison.Ordinal));
        Publisher.PublishesOf(qa.DeckId).ShouldBe(actions.Length);
        Relay.Refreshed.Count(deck => deck == qa.DeckId).ShouldBe(actions.Length);
    }

    private async Task<JsonElement> ActAsync(SeededQa qa, string action, int number)
    {
        var response = await Client.PostAsync($"/api/decks/{qa.DeckId}/qa/questions/{number}/{action}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await BodyAsync(response);
    }

    // Tries the action and reads what it answered, with what a refusal must not change before and after it.
    private async Task<Refused> RefuseAsync(SeededQa qa, string action, int number)
    {
        var before = await SnapshotAsync(qa);
        var response = await Client.PostAsync($"/api/decks/{qa.DeckId}/qa/questions/{number}/{action}", null);
        var body = await BodyAsync(response);
        var error = body.TryGetProperty("error", out var code) ? code.GetString() : null;
        return new Refused(response.StatusCode, error, before, await SnapshotAsync(qa));
    }

    private async Task<JsonElement> ViewAsync(SeededQa qa)
    {
        var response = await Client.GetAsync($"/api/decks/{qa.DeckId}/qa");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await BodyAsync(response);
    }

    // What a refused action must not change: the presenter's view (seq left out), the round's list revision, the deck's
    // activity, and the publishes and refreshes for the deck.
    private async Task<Snapshot> SnapshotAsync(SeededQa qa)
    {
        var view = JsonNode.Parse((await ViewAsync(qa)).GetRawText())!.AsObject();
        view.Remove("seq");
        return new Snapshot(
            view.ToJsonString(),
            Revisions.Current(qa.RoundId.ToString()),
            (await DeckSeed.ActivityAsync(Client, qa.DeckId)).Count,
            Publisher.PublishesOf(qa.DeckId),
            Relay.Refreshed.Count(deck => deck == qa.DeckId));
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record Snapshot(string View, long Revision, int Activity, int Published, int Refreshed);

    private sealed record Refused(HttpStatusCode Status, string? Error, Snapshot Before, Snapshot After);
}
