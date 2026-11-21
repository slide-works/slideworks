using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The per-slide switch, the deck's show_on setting and a clean start for every Go live (SW-9 D10, D15) on the real app,
// the real hub and the real presentation registry. Sam's deck (a quote and a Q&A slide) goes in over MCP; Sam presents
// from the presenter window (the test's Page) with the audience window as a second tab, and each guest is a phone-sized
// browser context that opens the join link the server gave.
public class Sw9SlideSwitchJourneyTests : AcceptanceTest
{
    private const int QUOTE = 1;
    private const int QA = 2;
    private const int SLIDES = 2;
    private const string QUESTION = "How do you split a big change?";

    // The first assertion after a hub push waits as long as the repo's other journeys do (15 s).
    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: with questions and reactions switched off on the Q&A slide, a phone on its Questions tab shows "Questions
    // are off on this slide. You can still upvote." with no Ask button and no reaction row while the presenter is on that
    // slide; on the other slide both are back, and moving to the Q&A slide again hides both again.
    [Test]
    public async Task Slide_WhenQuestionsAndReactionsOff_ShouldHideTheRowAndReplaceAsking()
    {
        var talk = await PresentAsync("Slide switch");
        await using var phoneContext = await OpenPhoneAsync();
        var phone = await JoinAsync(phoneContext, talk.JoinUrl);
        await phone.Questions.QuestionsTab.ClickAsync();
        await Expect(phone.Questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(phone.Questions.ListLoading).ToHaveCountAsync(0);
        await ExpectOnAsync(phone);
        await talk.Presenter.PressAsync("q");
        await Expect(talk.Panel.SettingsStrip).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await MoveToAsync(talk, QA);
        await Expect(talk.Panel.SlideQuestions).ToBeCheckedAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.SlideReactions).ToBeCheckedAsync();
        await talk.Panel.SlideQuestions.ClickAsync();
        await Expect(talk.Panel.SlideQuestions).ToBeCheckedAsync(new() { Checked = false });
        await talk.Panel.SlideReactions.ClickAsync();
        await Expect(talk.Panel.SlideReactions).ToBeCheckedAsync(new() { Checked = false });
        // The server holds both switches for slide 2.
        (await Eventually.ReadAsync(() => SlideSwitchAsync(talk.DeckId, QA), value => value == "false/false")).ShouldBe("false/false");
        await Expect(talk.Panel.SettingsError).ToHaveCountAsync(0);
        await ExpectOffAsync(phone);

        await MoveToAsync(talk, QUOTE);
        await ExpectOnAsync(phone);

        await MoveToAsync(talk, QA);
        await ExpectOffAsync(phone);
    }

    // The phone's Questions tab has its Ask button and no note, and its Slide tab has the reaction row.
    private async Task ExpectOnAsync(Phone phone)
    {
        await phone.Questions.QuestionsTab.ClickAsync();
        await Expect(phone.Questions.Ask).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(phone.Questions.ListOffHere).ToHaveCountAsync(0);
        await phone.Questions.SlideTab.ClickAsync();
        await Expect(phone.Questions.ReactionRow).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
    }

    // The note shows first (a positive proof the phone has re-read), then no Ask button and no reaction row.
    private async Task ExpectOffAsync(Phone phone)
    {
        await phone.Questions.QuestionsTab.ClickAsync();
        await Expect(phone.Questions.ListOffHere).ToContainTextAsync("Questions are off on this slide. You can still upvote.", new() { Timeout = PUSH_TIMEOUT });
        await Expect(phone.Questions.Ask).ToHaveCountAsync(0);
        await phone.Questions.SlideTab.ClickAsync();
        await Expect(phone.Questions.SlidePanel).ToBeVisibleAsync();
        await Expect(phone.Questions.ReactionRow).ToHaveCountAsync(0);
    }

    // Criteria: with "only on the Q&A slide" set on the deck page's card, a shown question has no card on the quote slide
    // and fills the Q&A slide in the audience window; after End and Go live the presenter's list and a new phone's list
    // are empty.
    [Test]
    public async Task ShowOn_WhenQaSlideOnly_ShouldShowTheQuestionOnlyOnTheQaSlideAndStartCleanNextSession()
    {
        var deckId = await CreateDeckAsync("Show on qa slide");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deckId);
        var card = new QaSettingsCard(Page);
        await Expect(card.ShowOnAnySlide).ToBeCheckedAsync(new() { Timeout = PUSH_TIMEOUT });
        await card.ShowOnQaSlide.ClickAsync();
        await Expect(card.ShowOnQaSlide).ToBeCheckedAsync();
        await Expect(card.Error).ToHaveCountAsync(0);
        (await Eventually.ReadAsync(() => ShowOnAsync(deckId), value => value == "qaSlide")).ShouldBe("qaSlide");

        var talk = await PresentDeckAsync(deckId);
        await talk.Presenter.PressAsync("q");
        await Expect(talk.Panel.EmptyOpen).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await using var phoneContext = await OpenPhoneAsync();
        var phone = await JoinAsync(phoneContext, talk.JoinUrl);
        await phone.Questions.QuestionsTab.ClickAsync();
        await Expect(phone.Questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await phone.Questions.Ask.ClickAsync();
        await phone.Questions.AskSheetText.FillAsync(QUESTION);
        await phone.Questions.AskSheetSend.ClickAsync();
        await Expect(phone.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Show(1)).ToContainTextAsync("Show on Q&A slide");

        await talk.Panel.Show(1).ClickAsync();

        // The phones know it is on screen.
        await Expect(phone.Questions.QuestionOnScreen(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Unshow(1)).ToBeVisibleAsync();

        await MoveToAsync(talk, QA);

        await Expect(talk.Screen.OneUp).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.OneUpText).ToHaveTextAsync(QUESTION);
        await Expect(talk.Screen.Card).ToHaveCountAsync(0);

        // The audience window now holds the shown question, so an empty card layer on the quote slide is a real answer.
        await MoveToAsync(talk, QUOTE);
        await Expect(talk.Screen.CardLayer).ToBeAttachedAsync();
        await Expect(talk.Screen.Card).ToHaveCountAsync(0);
        await Expect(phone.Questions.QuestionOnScreen(1)).ToBeVisibleAsync();

        await talk.Presenter.Live.End.ClickAsync();
        await Expect(talk.Presenter.Live.GoLive).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await talk.Presenter.Live.GoLive.ClickAsync();
        await Expect(talk.Presenter.Live.End).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        var newJoinUrl = await ReadAsync($"/api/decks/{deckId}/live", "joinUrl");

        await Expect(talk.Panel.EmptyOpen).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Row(1)).ToHaveCountAsync(0);
        await using var freshContext = await OpenPhoneAsync();
        var fresh = await JoinAsync(freshContext, newJoinUrl);
        await fresh.Questions.QuestionsTab.ClickAsync();
        await Expect(fresh.Questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(fresh.Questions.ListLoading).ToHaveCountAsync(0);
        await Expect(fresh.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(fresh.Questions.Question(1)).ToHaveCountAsync(0);
    }

    // The presenter moves one slide at a time and waits on the presenter's count and the audience window's layer.
    private async Task MoveToAsync(Talk talk, int slide)
    {
        await talk.Presenter.PressAsync(slide > 1 ? "ArrowRight" : "ArrowLeft");
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{slide} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Audience.Layer(slide)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });
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

    private async Task<Talk> PresentAsync(string title)
    {
        var deckId = await CreateDeckAsync(title);
        await SignIn.AsAsync(Page, TestUser.Sam);
        return await PresentDeckAsync(deckId);
    }

    // Sam opens the presenter window, goes live from its chip and opens the audience window beside it. The presenter is on
    // slide 1 when this returns.
    private async Task<Talk> PresentDeckAsync(string deckId)
    {
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var joinUrl = await ReadAsync($"/api/decks/{deckId}/live", "joinUrl");

        var audienceTab = await Context.NewPageAsync();
        var audience = new PlayerPage(audienceTab);
        await audienceTab.GotoAsync(await ReadAsync($"/api/decks/{deckId}/presenter", "audienceUrl"));
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Page.BringToFrontAsync();
        return new Talk(deckId, presenter, audience, new QaPanel(Page), new QaSlide(audience.Slide(QA)), joinUrl);
    }

    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(guest, new GuestQuestions(page));
    }

    private async Task<JsonElement> QaViewAsync(string deckId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/qa");
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    private async Task<string> ShowOnAsync(string deckId) =>
        (await QaViewAsync(deckId)).GetProperty("settings").GetProperty("showOn").GetString().ShouldNotBeNull();

    // A slide's two switches as the server holds them, "questions/reactions"; a slide with both on is not listed.
    private async Task<string> SlideSwitchAsync(string deckId, int slide)
    {
        var listed = (await QaViewAsync(deckId)).GetProperty("slides").EnumerateArray().Where(s => s.GetProperty("slide").GetInt32() == slide).ToArray();
        if (listed.Length == 0)
        {
            return "true/true";
        }

        return $"{listed[0].GetProperty("questions").GetBoolean()}/{listed[0].GetProperty("reactions").GetBoolean()}".ToLowerInvariant();
    }

    private async Task<string> ReadAsync(string path, string field)
    {
        var response = await Page.APIRequest.GetAsync(path);
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString().ShouldNotBeNull();
    }

    private Task<IBrowserContext> OpenPhoneAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    private sealed record Talk(string DeckId, PresenterPage Presenter, PlayerPage Audience, QaPanel Panel, QaSlide Screen, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);
}
