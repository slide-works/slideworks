using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The two Q&A tools an agent has (Q10), update_qa and get_questions, and what get_deck, the deck page and the template check
// say about a qa slide. update_qa runs over FakeQaSettings, so what it sent is what the fake recorded; every read runs over
// rows written by QaRowSeed. The app, and so the fake, is shared by the fixture: each test seeds a live deck of its own.
[TestFixture]
[CancelAfter(60000)]
public class QaMcpTests : QaTestFixture
{
    private const string DEFAULT_TEXT = "questions open · review first · names allowed · reactions on · shown over any slide";

    private FakeQaSettings Settings => Factory.Services.GetRequiredService<FakeQaSettings>();

    protected override void ConfigureQaServices(IServiceCollection services) => FakeQaSettings.AddTo(services);

    [TearDown]
    public void ForgetSettings() => Settings.Reset();

    [Test]
    public async Task UpdateQa_WhenReviewTrue_ShouldSendThatPatchAndSayIt()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, review = true });

        result.GetProperty("text").GetString().ShouldBe(DEFAULT_TEXT);
        result.GetProperty("settings").GetProperty("review").GetBoolean().ShouldBeTrue();
        result.GetProperty("settings").GetProperty("questions").GetString().ShouldBe("open");
        result.GetProperty("settings").GetProperty("showOn").GetString().ShouldBe("anySlide");
        (await Settings.GetAsync(qa.DeckId)).ShouldBe(QaSettings.Default with { Review = true });
        Settings.CallsFor(qa.DeckId).ShouldBe(["Update"]);
    }

    [Test]
    public async Task UpdateQa_WhenSeveralSettingsGiven_ShouldSayEachOfThem()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, questions = "closed", names = false, reactions = false, show_on = "qaSlide" });

        result.GetProperty("text").GetString().ShouldBe("questions closed · no review · names off · reactions off · shown on the Q&A slide only");
        (await Settings.GetAsync(qa.DeckId)).ShouldBe(new QaSettings(QaQuestions.Closed, false, false, false, QaShowOn.QaSlide));
    }

    [TestCase("review")]
    [TestCase("names")]
    [TestCase("show_on")]
    public async Task UpdateQa_WhenSlideGivenWithReview_ShouldRefuseByName(string other)
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var arguments = new Dictionary<string, object> { ["deck"] = qa.DeckId, ["slide"] = qa.QaSlide, ["reactions"] = false, [other] = OtherValue(other) };

        var error = await SlideCalls.ErrorAsync(agent, "update_qa", arguments);

        error.ShouldStartWith($"{other}: ");
        Settings.CallsFor(qa.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateQa_WhenSlideGiven_ShouldSetThatSlidesSwitchAndSayIt()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, slide = qa.QuoteSlide, questions = "off" });

        result.GetProperty("text").GetString().ShouldBe($"slide {qa.QuoteSlide}: questions off · reactions on");
        result.GetProperty("switch").GetProperty("questions").GetBoolean().ShouldBeFalse();
        result.GetProperty("switch").GetProperty("reactions").GetBoolean().ShouldBeTrue();
        Settings.SwitchOf(qa.DeckId, qa.QuoteSlide).ShouldBe(new QaSlideSwitch(false, true));
        Settings.CallsFor(qa.DeckId).ShouldBe([$"SetSlide {qa.QuoteSlide}"]);
    }

    [Test]
    public async Task UpdateQa_WhenDeckHasQuestionsOffAndSlideSwitchedOff_ShouldAcceptIt()
    {
        var qa = await SeedQaAsync();
        Settings.Set(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, slide = qa.QuoteSlide, questions = "off" });

        result.GetProperty("switch").GetProperty("questions").GetBoolean().ShouldBeFalse();
        Settings.SwitchOf(qa.DeckId, qa.QuoteSlide).Questions.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateQa_WhenNothingToChangeIsGiven_ShouldRefuseAndChangeNothing()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var deckError = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId });
        var slideError = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, slide = qa.QaSlide });

        deckError.ShouldStartWith("give at least one setting");
        slideError.ShouldStartWith("give questions or reactions");
        Settings.CallsFor(qa.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateQa_WhenAValueIsUnknownOrSlideIsOutside_ShouldRefuseNamingTheArgument()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var questions = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, questions = "maybe" });
        var showOn = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, show_on = "everywhere" });
        var closed = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, slide = qa.QaSlide, questions = "closed" });
        var outside = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, slide = 9, reactions = false });
        var justPast = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, slide = 3, reactions = false });
        var zero = await SlideCalls.ErrorAsync(agent, "update_qa", new { deck = qa.DeckId, slide = 0, reactions = false });

        questions.ShouldBe("questions: must be one of 'open', 'closed', 'off'");
        showOn.ShouldBe("show_on: must be one of 'anySlide', 'qaSlide'");
        closed.ShouldBe("questions: with slide, must be one of 'open', 'off'");
        outside.ShouldBe("slide: must be between 1 and 2");
        justPast.ShouldBe("slide: must be between 1 and 2");
        zero.ShouldBe("slide: must be between 1 and 2");
        Settings.CallsFor(qa.DeckId).ShouldBeEmpty();
    }

    [TestCase("update_qa")]
    [TestCase("get_questions")]
    public async Task UpdateQa_WhenCallerCannotEdit_ShouldBeRefused(string tool)
    {
        var qa = await SeedQaAsync();
        await SharingApi.PutOkAsync(Client, qa.DeckId, "link");
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var arguments = tool == "update_qa"
            ? new Dictionary<string, object> { ["deck"] = qa.DeckId, ["review"] = true }
            : new Dictionary<string, object> { ["deck"] = qa.DeckId };

        var error = await SlideCalls.ErrorAsync(alex, tool, arguments);

        error.ShouldBe("Deck not found");
        Settings.CallsFor(qa.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task GetQuestions_WhenCallerCannotViewTheDeck_ShouldBeRefused()
    {
        var qa = await SeedQaAsync();
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);

        var error = await SlideCalls.ErrorAsync(alex, "get_questions", new { deck = qa.DeckId });

        error.ShouldBe("Deck not found");
    }

    [Test]
    public async Task GetQuestions_WhenStateOpen_ShouldListOnlyOpenQuestionsWithVotes()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?", name: "Priya");
        await QaRowSeed.QuestionAsync(qa.RoundId, "Do feature flags count as shipping?", QuestionState.Answered);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who reviews the slices?", QuestionState.Waiting);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Why is this talk so long?", hidden: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "What is the smallest slice you have shipped?");
        await QaRowSeed.VoteAsync(qa.RoundId, "voter-1", 1, 2, 5);
        await QaRowSeed.VoteAsync(qa.RoundId, "voter-2", 1, 5);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var report = await SlideCalls.OkAsync(agent, "get_questions", new { deck = qa.DeckId, state = "open" });

        var session = report.GetProperty("sessions").EnumerateArray().Single();
        session.GetProperty("asked").GetInt32().ShouldBe(5);
        session.GetProperty("answered").GetInt32().ShouldBe(1);
        session.GetProperty("hidden").GetInt32().ShouldBe(1);
        var questions = session.GetProperty("questions").EnumerateArray().ToList();
        questions.Select(q => q.GetProperty("text").GetString()).ShouldBe(["How do you split a big change?", "What is the smallest slice you have shipped?"]);
        questions.Select(q => q.GetProperty("votes").GetInt32()).ShouldBe([2, 2]);
        questions.Select(q => q.GetProperty("state").GetString()).ShouldBe(["open", "open"]);
        questions[0].GetProperty("name").GetString().ShouldBe("Priya");
        questions[1].GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task GetQuestions_WhenNoStateGiven_ShouldListEveryQuestionWithHiddenWinningOverItsState()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Open one");
        await QaRowSeed.QuestionAsync(qa.RoundId, "Hidden answered one", QuestionState.Answered, hidden: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Waiting one", QuestionState.Waiting);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var all = await SlideCalls.OkAsync(agent, "get_questions", new { deck = qa.DeckId });
        var hidden = await SlideCalls.OkAsync(agent, "get_questions", new { deck = qa.DeckId, state = "hidden" });

        all.GetProperty("sessions")[0].GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("state").GetString()).ShouldBe(["open", "hidden", "waiting"]);
        all.GetProperty("sessions")[0].GetProperty("answered").GetInt32().ShouldBe(0);
        hidden.GetProperty("sessions")[0].GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("text").GetString()).ShouldBe(["Hidden answered one"]);
    }

    [Test]
    public async Task GetQuestions_WhenStateIsUnknown_ShouldRefuseNamingIt()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var error = await SlideCalls.ErrorAsync(agent, "get_questions", new { deck = qa.DeckId, state = "shown" });

        error.ShouldBe("state: must be one of 'open', 'waiting', 'answered', 'hidden'");
    }

    [Test]
    public async Task GetQuestions_WhenTheDeckWentLiveTwice_ShouldListTheNewestSessionFirst()
    {
        var first = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(first.RoundId, "Asked in the first session");
        (await Client.DeleteAsync($"/api/decks/{first.DeckId}/live")).EnsureSuccessStatusCode();
        Clock.Advance(TimeSpan.FromHours(1));
        var second = await GoLiveAgainAsync(first.DeckId);
        await QaRowSeed.QuestionAsync(second, "Asked in the second session");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var report = await SlideCalls.OkAsync(agent, "get_questions", new { deck = first.DeckId });

        var sessions = report.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(s => s.GetProperty("questions")[0].GetProperty("text").GetString()).ShouldBe(["Asked in the second session", "Asked in the first session"]);
        sessions[0].GetProperty("startedAt").GetDateTimeOffset().ShouldBeGreaterThan(sessions[1].GetProperty("startedAt").GetDateTimeOffset());
    }

    [Test]
    public async Task GetQuestions_WhenQuestionsAreNowOff_ShouldStillReportTheStoredQuestions()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Asked before it went off");
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var report = await SlideCalls.OkAsync(agent, "get_questions", new { deck = qa.DeckId });

        report.GetProperty("sessions")[0].GetProperty("questions")[0].GetProperty("text").GetString().ShouldBe("Asked before it went off");
    }

    [Test]
    public async Task GetQuestions_WhenCalled_ShouldCarryNoGuestId()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Who is asking?", guestId: "asker-guest-7f3a", name: "Priya");
        await QaRowSeed.VoteAsync(qa.RoundId, "voter-guest-9c1d", 1);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (_, report) = await agent.CallToolAsync("get_questions", new { deck = qa.DeckId });

        var raw = report.GetRawText();
        raw.ShouldNotContain("asker-guest-7f3a");
        raw.ShouldNotContain("voter-guest-9c1d");
        var question = report.GetProperty("sessions")[0].GetProperty("questions")[0];
        question.EnumerateObject().Select(p => p.Name).ShouldBe(["text", "name", "state", "votes", "askedAt"]);
        report.GetProperty("sessions")[0].EnumerateObject().Select(p => p.Name).ShouldBe(["startedAt", "asked", "answered", "hidden", "questions"]);
    }

    [Test]
    public async Task GetDeck_WhenDeckHasSettings_ShouldCarryThem()
    {
        var qa = await SeedQaAsync();
        var settings = new QaSettings(QaQuestions.Closed, true, false, true, QaShowOn.QaSlide);
        await QaRowSeed.SettingsAsync(qa.DeckId, settings);
        await QaRowSeed.ReactionAsync(qa.SessionId, qa.QuoteSlideId, ReactionEmoji.Heart, 4);
        await QaRowSeed.ReactionAsync(qa.SessionId, qa.QuoteSlideId, ReactionEmoji.Clap, 2);
        await QaRowSeed.ReactionAsync(qa.SessionId, qa.QaSlideId, ReactionEmoji.ThumbsUp, 7);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var deck = await SlideCalls.OkAsync(agent, "get_deck", new { deck = qa.DeckId });

        var carried = deck.GetProperty("qa");
        carried.GetProperty("questions").GetString().ShouldBe("closed");
        carried.GetProperty("review").GetBoolean().ShouldBeTrue();
        carried.GetProperty("names").GetBoolean().ShouldBeFalse();
        carried.GetProperty("reactions").GetBoolean().ShouldBeTrue();
        carried.GetProperty("showOn").GetString().ShouldBe("qaSlide");
        var slides = deck.GetProperty("reactions").EnumerateArray().Single().GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("slide").GetInt32()).ShouldBe([qa.QuoteSlide, qa.QaSlide]);
        slides[0].GetProperty("counts").GetProperty("heart").GetInt32().ShouldBe(4);
        slides[0].GetProperty("counts").GetProperty("clap").GetInt32().ShouldBe(2);
        slides[1].GetProperty("counts").GetProperty("thumbsUp").GetInt32().ShouldBe(7);
    }

    [Test]
    public async Task GetDeck_WhenDeckWentLiveTwice_ShouldListNewestSessionFirstEachWithItsOwnTaps()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.ReactionAsync(qa.SessionId, qa.QuoteSlideId, ReactionEmoji.Heart, 3);
        (await Client.DeleteAsync($"/api/decks/{qa.DeckId}/live")).EnsureSuccessStatusCode();
        Clock.Advance(TimeSpan.FromHours(1));
        var second = await GoLiveAgainSessionAsync(qa.DeckId);
        await QaRowSeed.ReactionAsync(second, qa.QuoteSlideId, ReactionEmoji.Laugh, 5);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var deck = await SlideCalls.OkAsync(agent, "get_deck", new { deck = qa.DeckId });

        var sessions = deck.GetProperty("reactions").EnumerateArray().ToList();
        sessions.Count.ShouldBe(2);
        sessions[0].GetProperty("startedAt").GetDateTimeOffset().ShouldBeGreaterThan(sessions[1].GetProperty("startedAt").GetDateTimeOffset());
        var newest = sessions[0].GetProperty("slides").EnumerateArray().Single().GetProperty("counts");
        newest.GetProperty("laugh").GetInt32().ShouldBe(5);
        newest.GetProperty("heart").GetInt32().ShouldBe(0);
        var oldest = sessions[1].GetProperty("slides").EnumerateArray().Single().GetProperty("counts");
        oldest.GetProperty("heart").GetInt32().ShouldBe(3);
        oldest.GetProperty("laugh").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task GetDeck_WhenDeckHasNoSettingsOrReactions_ShouldCarryTheDefaultsAndNoSessions()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await SlideCalls.OkAsync(agent, "get_deck", new { deck });

        result.GetProperty("qa").GetProperty("questions").GetString().ShouldBe("open");
        result.GetProperty("qa").GetProperty("review").GetBoolean().ShouldBeFalse();
        result.GetProperty("reactions").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task ListTools_WhenCalled_ShouldIncludeTheTwoNewTools()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var names = (await agent.Client.ListToolsAsync()).Select(t => t.Name).ToList();

        names.ShouldContain("update_qa");
        names.ShouldContain("get_questions");
        names.ShouldNotContain("approve_question");
        names.ShouldNotContain("hide_question");
        names.ShouldNotContain("answer_question");
        names.ShouldNotContain("show_question");
    }

    [Test]
    public async Task DeckPage_WhenQaSlideHasATitleOrNot_ShouldUseItOrTheDefaultAsItsHeadline()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "qa", "{}");
        await DeckSeed.AddSlideAsync(agent, deck, "qa", """{ "title": "Ask us anything" }""");

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        page.GetProperty("slides")[0].GetProperty("headline").GetString().ShouldBe("Questions from the room");
        page.GetProperty("slides")[1].GetProperty("headline").GetString().ShouldBe("Ask us anything");
    }

    // A qa slide is for the live room, not for a kind of talk, so no template warns about it.
    [Test]
    public async Task AddSlide_WhenQaGoesInADeckOfAnyTemplate_ShouldRaiseNoTemplateWarning()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        foreach (var template in new TemplateCatalogue().All)
        {
            var deck = (await SlideCalls.OkAsync(agent, "create_deck", new { title = "Ship smaller", template = template.Name })).GetProperty("deck").GetString()!;

            var added = await DeckSeed.AddSlideAsync(agent, deck, "qa", "{}");
            var updated = await SlideCalls.UpdateAsync(agent, deck, 1, added.GetProperty("revision").GetInt64(), """{ "title": "Ask us anything" }""");

            added.GetProperty("warnings").GetArrayLength().ShouldBe(0, template.Name);
            updated.GetProperty("warnings").GetArrayLength().ShouldBe(0, template.Name);
        }
    }

    private static object OtherValue(string setting) => setting == "show_on" ? "qaSlide" : (object)true;

    // Go live again after the first session ended: a new session, and a Q&A round for it when Go live opened none.
    private async Task<Guid> GoLiveAgainSessionAsync(string deckId)
    {
        var response = await Client.PostAsync($"/api/decks/{deckId}/live", null);
        response.EnsureSuccessStatusCode();
        return Guid.Parse((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!);
    }

    private async Task<Guid> GoLiveAgainAsync(string deckId)
    {
        var sessionId = await GoLiveAgainSessionAsync(deckId);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var open = await db.PollRounds.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync();
        return open ?? (await QaRowSeed.RoundAsync(deckId, sessionId)).Id;
    }
}
