using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Review first and hiding on the real app (SW-9 D4, D13), through the real hub. Sam's deck (a quote and a Q&A slide) goes
// in over MCP; Sam presents from the test's Page and goes live; each guest is its own phone-sized browser context that
// opens the join link the server gave, then the Questions tab. Every step waits on what the screens show: the panel's
// row for the room's side, the phone's list for the guest's.
public class Sw9ModerationJourneyTests : AcceptanceTest
{
    private const string QUESTION = "How do you split a big change?";

    // The first assertion after a hub push waits as long as the repo's other journeys do (15 s), since several browser
    // contexts share a loaded host.
    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: with review on, the asker's phone shows its own question as "Waiting for ..." and another phone shows
    // nothing, while the panel holds the question under To review; Approve makes it an open question on the other phone.
    [Test]
    public async Task Review_WhenOn_ShouldHoldAQuestionUntilApproved()
    {
        var talk = await PresentAsync("Review first");
        await talk.Panel.SettingsReview.ClickAsync();
        await Expect(talk.Panel.SettingsReview).ToHaveTextAsync("Review first", new() { Timeout = PUSH_TIMEOUT });
        await using var askerContext = await OpenPhoneAsync();
        var asker = await JoinAsync(askerContext, talk.JoinUrl);
        await using var otherContext = await OpenPhoneAsync();
        var other = await JoinAsync(otherContext, talk.JoinUrl);
        await Expect(other.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await AskAsync(asker, QUESTION);

        await Expect(asker.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION);
        await Expect(asker.Questions.QuestionWaiting(1)).ToContainTextAsync("Waiting for");
        await Expect(talk.Panel.TabReview).ToContainTextAsync("1", new() { Timeout = PUSH_TIMEOUT });
        await talk.Panel.TabReview.ClickAsync();
        await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(QUESTION);
        // The server has the question and has told the room's side; the other phone still holds nothing and offers nothing.
        await Expect(other.Questions.Question(1)).ToHaveCountAsync(0);
        await Expect(other.Questions.ListEmpty).ToBeVisibleAsync();
        await Expect(other.Questions.QuestionsBadge).ToHaveCountAsync(0);

        await talk.Panel.Approve(1).ClickAsync();

        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.QuestionWaiting(1)).ToHaveCountAsync(0);
        await Expect(other.Questions.QuestionVotes(1)).ToHaveTextAsync("1");
        await Expect(asker.Questions.QuestionWaiting(1)).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.QuestionYou(1)).ToBeVisibleAsync();
        await Expect(talk.Panel.Approve(1)).ToHaveCountAsync(0);
    }

    // Criteria: Hide takes an open, upvoted question out of the room: the asker's phone says "Not shown to the room" and
    // the other phone shows nothing; Restore brings it back on the other phone with its two votes.
    [Test]
    public async Task Hide_WhenPressedThenRestored_ShouldLeaveTheRoomAndComeBackWithItsVotes()
    {
        var talk = await PresentAsync("Hide and restore");
        await using var askerContext = await OpenPhoneAsync();
        var asker = await JoinAsync(askerContext, talk.JoinUrl);
        await using var otherContext = await OpenPhoneAsync();
        var other = await JoinAsync(otherContext, talk.JoinUrl);

        await AskAsync(asker, QUESTION);
        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await other.Questions.QuestionUpvote(1).ClickAsync();
        await Expect(other.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(other.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
        await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("2", new() { Timeout = PUSH_TIMEOUT });

        await talk.Panel.Hide(1).ClickAsync();

        await Expect(asker.Questions.QuestionHidden(1)).ToHaveTextAsync("Not shown to the room", new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION);
        await Expect(other.Questions.Question(1)).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.ListEmpty).ToBeVisibleAsync();
        await talk.Panel.TabHidden.ClickAsync();
        await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(QUESTION);

        await talk.Panel.Restore(1).ClickAsync();

        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
        await Expect(other.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(asker.Questions.QuestionHidden(1)).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(asker.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
    }

    // Sends a question from the phone's Questions tab and waits until the sheet has closed on the sent question.
    private async Task AskAsync(Phone phone, string text)
    {
        await phone.Questions.Ask.ClickAsync();
        await phone.Questions.AskSheetText.FillAsync(text);
        await phone.Questions.AskSheetSend.ClickAsync();
        await Expect(phone.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
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
        return new Talk(panel, joinUrl);
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

    private sealed record Talk(QaPanel Panel, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);
}
