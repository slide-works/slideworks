using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-9's harder journeys on the real app, the real hub and the real presentation registry: a question typed while the
// phone's connection is down, and an agent reading the room's questions over MCP. Sam's deck (a quote and a Q&A slide) goes
// in over MCP; Sam presents from the test's Page and goes live; each guest is its own phone-sized browser context that opens
// the join link the server gave, then the Questions tab. A dropped connection is DroppableHub: it closes the phone's hub
// socket and holds its reconnect until released, which a finally always does.
public class Sw9TroubleJourneyTests : AcceptanceTest
{
    private const string QUESTION = "How do you split a big change?";

    // The first assertion after a hub push waits as long as the repo's other journeys do (15 s), since several browser
    // contexts share a loaded host.
    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: with the phone's connection down, a typed question stays in the ask sheet under "Not sent yet" and nothing
    // reaches the room; once the connection is back the sheet closes, the question shows on the phone and on the panel once,
    // and the agent's read counts one question.
    [Test]
    public async Task Question_WhenTypedOffline_ShouldSendOnReconnect()
    {
        var talk = await PresentAsync("Question offline");
        await using var phoneContext = await OpenPhoneAsync();
        var (phone, hub) = await JoinDroppableAsync(phoneContext, talk.JoinUrl);
        try
        {
            await Expect(talk.Panel.EmptyOpen).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
            await phone.Questions.Ask.ClickAsync();
            await phone.Questions.AskSheetText.FillAsync(QUESTION);
            (await hub.DropAsync()).ShouldBe(1, "the phone should have one hub connection to drop");
            await Expect(phone.Guest.Reconnecting).ToBeVisibleAsync();

            await phone.Questions.AskSheetSend.ClickAsync();

            await Expect(phone.Questions.AskSheetQueued).ToBeVisibleAsync();
            await Expect(phone.Questions.AskSheetQueued).ToContainTextAsync("Not sent yet");
            await Expect(phone.Questions.AskSheetText).ToHaveValueAsync(QUESTION);
            await Expect(talk.Panel.Row(1)).ToHaveCountAsync(0);
            await Expect(talk.Panel.EmptyOpen).ToBeVisibleAsync();

            hub.Release();

            await Expect(phone.Guest.Reconnecting).ToBeHiddenAsync(new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Questions.QuestionYou(1)).ToBeVisibleAsync();
            await Expect(phone.Questions.QuestionVotes(1)).ToHaveTextAsync("1");
            await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("1 vote");
            // The server's own count proves it arrived once, after the page has shown it.
            await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
            var report = await AgentSeed.CallAsync(agent, "get_questions", new() { ["deck"] = talk.DeckId });
            var session = report.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
            session.GetProperty("asked").GetInt32().ShouldBe(1);
            session.GetProperty("questions").EnumerateArray().ShouldHaveSingleItem().GetProperty("text").GetString().ShouldBe(QUESTION);
            await Expect(phone.Questions.Question(2)).ToHaveCountAsync(0);
            await Expect(talk.Panel.Row(2)).ToHaveCountAsync(0);
        }
        finally
        {
            hub.Release();
        }
    }

    // Criteria: Sam's agent turns review on with update_qa; a guest asks and the agent reads the question as waiting with
    // one vote; the presenter approves it, a second guest upvotes it, and the agent reads it as open with two votes and no
    // name. Nothing the agent reads carries either guest's id.
    [Test]
    public async Task Agent_WhenReadingQuestions_ShouldSeeVotesAndStatesAndNoGuests()
    {
        var talk = await PresentAsync("Agent reads questions");
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var updated = await AgentSeed.CallAsync(agent, "update_qa", new() { ["deck"] = talk.DeckId, ["review"] = true });
        updated.GetProperty("settings").GetProperty("review").GetBoolean().ShouldBeTrue();
        await Expect(talk.Panel.SettingsReview).ToHaveTextAsync("Review first", new() { Timeout = PUSH_TIMEOUT });
        await using var askerContext = await OpenPhoneAsync();
        var asker = await JoinAsync(askerContext, talk.JoinUrl);
        await using var otherContext = await OpenPhoneAsync();
        var other = await JoinAsync(otherContext, talk.JoinUrl);

        await asker.Questions.Ask.ClickAsync();
        await asker.Questions.AskSheetText.FillAsync(QUESTION);
        await asker.Questions.AskSheetSend.ClickAsync();
        await Expect(asker.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.QuestionWaiting(1)).ToContainTextAsync("Waiting for");
        await Expect(talk.Panel.TabReview).ToContainTextAsync("1", new() { Timeout = PUSH_TIMEOUT });

        var waiting = await agent.CallToolAsync("get_questions", new Dictionary<string, object?> { ["deck"] = talk.DeckId });
        var waitingText = RawText(waiting);
        var waitingQuestion = OnlyQuestion(JsonDocument.Parse(waitingText).RootElement);
        waitingQuestion.GetProperty("text").GetString().ShouldBe(QUESTION);
        waitingQuestion.GetProperty("state").GetString().ShouldBe("waiting");
        waitingQuestion.GetProperty("votes").GetInt32().ShouldBe(1);

        await talk.Panel.Approve(1).ClickAsync();
        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await other.Questions.QuestionUpvote(1).ClickAsync();
        await Expect(other.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(other.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
        await talk.Panel.TabOpen.ClickAsync();
        await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("2 votes", new() { Timeout = PUSH_TIMEOUT });

        var open = await agent.CallToolAsync("get_questions", new Dictionary<string, object?> { ["deck"] = talk.DeckId });
        var openText = RawText(open);
        var openQuestion = OnlyQuestion(JsonDocument.Parse(openText).RootElement);
        openQuestion.GetProperty("text").GetString().ShouldBe(QUESTION);
        openQuestion.GetProperty("state").GetString().ShouldBe("open");
        openQuestion.GetProperty("votes").GetInt32().ShouldBe(2);
        openQuestion.GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        var filtered = await AgentSeed.CallAsync(agent, "get_questions", new() { ["deck"] = talk.DeckId, ["state"] = "waiting" });
        filtered.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem().GetProperty("questions").GetArrayLength().ShouldBe(0);
        foreach (var context in new[] { askerContext, otherContext })
        {
            var guestId = (await context.CookiesAsync()).Single(c => c.Name == "sw_guest").Value;
            guestId.ShouldNotBeNullOrEmpty();
            waitingText.ShouldNotContain(guestId);
            openText.ShouldNotContain(guestId);
        }
    }

    private static JsonElement OnlyQuestion(JsonElement report) =>
        report.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem().GetProperty("questions").EnumerateArray().ShouldHaveSingleItem();

    private static string RawText(ModelContextProtocol.Protocol.CallToolResult result)
    {
        (result.IsError ?? false).ShouldBeFalse();
        return result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text;
    }

    // Sam's deck over MCP, the presenter window gone live and its Questions panel open. The presenter is on slide 1.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "qa", ["slots"] = new { } });
        }

        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/live");
        response.Status.ShouldBe(200, await response.TextAsync());
        var joinUrl = JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();

        var panel = new QaPanel(Page);
        await presenter.PressAsync("q");
        await Expect(panel.Root).ToBeVisibleAsync();
        await Expect(panel.SettingsStrip).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        return new Talk(deckId, panel, joinUrl);
    }

    // A guest scans the code, follows the presenter and opens the Questions tab, whose list has loaded when this returns.
    private async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        return await OpenQuestionsAsync(page, joinUrl);
    }

    // As JoinAsync, on a phone whose hub connection the test can drop (set up before the page navigates).
    private async Task<(Phone Phone, DroppableHub Hub)> JoinDroppableAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var hub = await DroppableHub.RouteAsync(page);
        return (await OpenQuestionsAsync(page, joinUrl), hub);
    }

    private async Task<Phone> OpenQuestionsAsync(IPage page, string joinUrl)
    {
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        var questions = new GuestQuestions(page);
        await questions.QuestionsTab.ClickAsync();
        await Expect(questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(questions.ListLoading).ToHaveCountAsync(0);
        return new Phone(guest, questions);
    }

    private Task<IBrowserContext> OpenPhoneAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    private sealed record Talk(string DeckId, QaPanel Panel, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);
}
