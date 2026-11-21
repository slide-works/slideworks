using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The deck-level questions setting on the real app (SW-9 D2, D15), through the real hub. Sam's deck (a quote and a Q&A
// slide) goes in over MCP. Closed is set from the presenter's Questions panel while live; Off is set on the deck page's
// "Q&A and reactions" card. Each guest is its own phone-sized browser context that opens the join link the server gave.
public class Sw9QuestionsSettingJourneyTests : AcceptanceTest
{
    private const string QUESTION = "How do you split a big change?";

    // The first assertion after a hub push waits as long as the repo's other journeys do (15 s).
    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: when the presenter closes questions from the strip, the phone shows the closed note and no Ask button,
    // the panel's label says closed, and another guest's upvote still counts (the panel reads the new total).
    [Test]
    public async Task Questions_WhenClosedMidTalk_ShouldStopAskingAndKeepUpvotes()
    {
        var deckId = await CreateDeckAsync("Closed mid talk");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var joinUrl = await JoinUrlAsync(deckId);
        var panel = new QaPanel(Page);
        await presenter.PressAsync("q");
        await Expect(panel.Root).ToBeVisibleAsync();
        await Expect(panel.SettingsStrip).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(panel.SettingsQuestions).ToHaveTextAsync("Questions open");

        await using var askerContext = await OpenPhoneAsync();
        var asker = await JoinAsync(askerContext, joinUrl);
        await using var otherContext = await OpenPhoneAsync();
        var other = await JoinAsync(otherContext, joinUrl);
        await asker.Questions.Ask.ClickAsync();
        await asker.Questions.AskSheetText.FillAsync(QUESTION);
        await asker.Questions.AskSheetSend.ClickAsync();
        await Expect(asker.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.Ask).ToBeVisibleAsync();

        await panel.SettingsQuestions.ClickAsync();

        await Expect(panel.SettingsQuestions).ToHaveTextAsync("Questions closed", new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.ListClosed).ToContainTextAsync("has closed questions. You can still upvote.", new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.Ask).ToHaveCountAsync(0);
        await Expect(asker.Questions.ListClosed).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.Ask).ToHaveCountAsync(0);
        // The server holds the closed state: the setting reads back as closed.
        (await QuestionsSettingAsync(deckId)).ShouldBe("closed");

        await other.Questions.QuestionUpvote(1).ClickAsync();

        await Expect(other.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(other.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
        await Expect(other.Questions.ListError).ToHaveCountAsync(0);
        await Expect(panel.RowVotes(1)).ToContainTextAsync("2", new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.QuestionVotes(1)).ToHaveTextAsync("2", new() { Timeout = PUSH_TIMEOUT });
    }

    // Criteria: with questions off on the deck page's card before going live, a phone that joins has no Questions tab and
    // no tab bar; turning it back to open while live brings the tab, and the list opens with its Ask button.
    [Test]
    public async Task Questions_WhenOffForTheDeck_ShouldShowNoQuestionsTab()
    {
        var deckId = await CreateDeckAsync("Off for the deck");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deckId);
        var card = new QaSettingsCard(Page);
        await Expect(card.QuestionsOpen).ToBeCheckedAsync(new() { Timeout = PUSH_TIMEOUT });
        await card.QuestionsOff.ClickAsync();
        await Expect(card.QuestionsOff).ToBeCheckedAsync();
        await Expect(card.Error).ToHaveCountAsync(0);
        // Proved by reading it back, not by the control alone.
        (await Eventually.ReadAsync(() => QuestionsSettingAsync(deckId), value => value == "off")).ShouldBe("off");
        var response = await Page.APIRequest.PostAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        var joinUrl = await JoinUrlAsync(deckId);

        await using var phoneContext = await OpenPhoneAsync();
        var page = await phoneContext.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        var questions = new GuestQuestions(page);
        await Expect(guest.Following).ToBeVisibleAsync();
        await Expect(questions.QuestionsTab).ToHaveCountAsync(0);
        await Expect(questions.TabBar).ToHaveCountAsync(0);

        await card.QuestionsOpen.ClickAsync();

        await Expect(card.QuestionsOpen).ToBeCheckedAsync();
        await Expect(questions.QuestionsTab).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await questions.QuestionsTab.ClickAsync();
        await Expect(questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(questions.Ask).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
    }

    private async Task<string> CreateDeckAsync(string title)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
        var deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "qa", ["slots"] = new { } });
        return deckId;
    }

    // The deck's questions setting as the server holds it, from the presenter's control view.
    private async Task<string> QuestionsSettingAsync(string deckId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/qa");
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("settings").GetProperty("questions").GetString().ShouldNotBeNull();
    }

    private async Task<string> JoinUrlAsync(string deckId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/live");
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();
    }

    // A guest scans the code, follows the presenter and opens the Questions tab, whose list has loaded when this returns.
    private async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
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

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);
}
