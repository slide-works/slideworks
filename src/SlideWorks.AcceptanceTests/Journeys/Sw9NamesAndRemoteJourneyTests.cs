using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Names and the phone remote's questions strip on the real app (SW-9 D3, Q9), through the real hub. Sam presents from the
// test's Page with the audience window beside it; guests are phone-sized browser contexts that open the join link; the
// remote is a second context of Sam at a phone viewport on /remote/{id}.
public class Sw9NamesAndRemoteJourneyTests : AcceptanceTest
{
    private const int QA = 2;
    private const int SLIDES = 2;
    private const string NAMED = "How do you split a big change?";
    private const string ANON = "What do you cut first?";

    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: with names on, a guest's named question shows the name on another phone and in the panel; once the panel's
    // Names switch is off, the sheet has no name row and a new question is "Anonymous" on both.
    [Test]
    public async Task Names_WhenOnAndOff_ShouldShowTheNameOrOfferNone()
    {
        var talk = await PresentAsync("Names journey");
        await talk.Presenter.PressAsync("q");
        await Expect(talk.Panel.Root).ToBeVisibleAsync();
        await Expect(talk.Panel.SettingsNames).ToContainTextAsync("Names allowed", new() { Timeout = PUSH_TIMEOUT });
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await first.Questions.QuestionsTab.ClickAsync();
        await second.Questions.QuestionsTab.ClickAsync();
        await Expect(first.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await first.Questions.Ask.ClickAsync();
        await Expect(first.Questions.AskSheetNameRow).ToBeVisibleAsync();
        await first.Questions.AskSheetText.FillAsync(NAMED);
        await first.Questions.AskSheetNameSwitch.ClickAsync();
        await Expect(first.Questions.AskSheetNameSwitch).ToHaveAttributeAsync("aria-checked", "true");
        await first.Questions.AskSheetName.FillAsync("Priya");
        await Expect(first.Questions.AskSheetSend).ToContainTextAsync("Send as Priya");
        await first.Questions.AskSheetSend.ClickAsync();
        await Expect(first.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });

        await Expect(second.Questions.QuestionText(1)).ToHaveTextAsync(NAMED, new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Questions.QuestionMeta(1)).ToContainTextAsync("Priya");
        await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(NAMED, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.RowMeta(1)).ToContainTextAsync("Priya");

        await talk.Panel.SettingsNames.ClickAsync();
        await Expect(talk.Panel.SettingsNames).ToContainTextAsync("Names off", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.SettingsError).ToHaveCountAsync(0);

        // The first phone still has "Show my name" on and "Priya" typed (the sheet keeps both across closes). With names off the
        // row is gone and the question goes out anonymously; a stale name would come back refused and keep the sheet open.
        await first.Questions.Ask.ClickAsync();
        await Expect(first.Questions.AskSheet).ToBeVisibleAsync();
        await Expect(first.Questions.AskSheetNameRow).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await first.Questions.AskSheetText.FillAsync(ANON);
        await Expect(first.Questions.AskSheetSend).ToContainTextAsync("anonymously");
        await first.Questions.AskSheetSend.ClickAsync();
        await Expect(first.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });

        await Expect(talk.Panel.RowText(2)).ToHaveTextAsync(ANON, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.RowMeta(2)).ToContainTextAsync("Anonymous");
        await Expect(talk.Panel.RowMeta(1)).ToContainTextAsync("Priya");
        await Expect(second.Questions.QuestionText(2)).ToHaveTextAsync(ANON, new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Questions.QuestionMeta(2)).ToContainTextAsync("Anonymous");
        await Expect(second.Questions.QuestionMeta(1)).ToContainTextAsync("Priya");
        await Expect(first.Questions.AskSheetNameRow).ToHaveCountAsync(0);
    }

    // Criteria: on the presenter's phone remote, the questions strip shows the top open question; Show puts it on the
    // audience window and Answered takes it off and leaves the strip empty.
    [Test]
    public async Task Remote_WhenShowAndAnsweredPressed_ShouldDriveTheBigScreen()
    {
        var talk = await PresentAsync("Remote journey");
        await using var guestPhone = await OpenPhoneAsync();
        var guest = await JoinAsync(guestPhone, talk.JoinUrl);
        await using var otherPhone = await OpenPhoneAsync();
        var other = await JoinAsync(otherPhone, talk.JoinUrl);
        await guest.Questions.QuestionsTab.ClickAsync();
        await other.Questions.QuestionsTab.ClickAsync();
        await Expect(guest.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(other.Questions.ListEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await AskAsync(guest, NAMED);
        await Expect(other.Questions.QuestionText(1)).ToHaveTextAsync(NAMED, new() { Timeout = PUSH_TIMEOUT });
        await AskAsync(other, ANON);
        await Expect(guest.Questions.QuestionText(2)).ToHaveTextAsync(ANON, new() { Timeout = PUSH_TIMEOUT });
        // The earlier question was asked first, so asking order alone would pick it; the later one gets the extra vote.
        await guest.Questions.QuestionUpvote(2).ClickAsync();
        await talk.Presenter.PressAsync("q");
        await Expect(talk.Panel.RowVotes(2)).ToContainTextAsync("2", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("1");

        var options = ContextOptions();
        options.ViewportSize = new ViewportSize { Width = 390, Height = 844 };
        options.IsMobile = true;
        options.HasTouch = true;
        await using var remoteContext = await Browser.NewContextAsync(options);
        var remotePage = await remoteContext.NewPageAsync();
        await SignIn.AsAsync(remotePage, TestUser.Sam);
        var speaker = new SpeakerPage(remotePage);
        await speaker.GotoAsync(talk.DeckId);
        await speaker.IsLiveAsync();
        var strip = new QaStrip(remotePage);
        await Expect(strip.Text).ToHaveTextAsync(ANON, new() { Timeout = PUSH_TIMEOUT });
        await Expect(strip.Counts).ToContainTextAsync("2 open");
        await Expect(talk.Screen.Card).ToHaveCountAsync(0);

        await strip.Show.ClickAsync();

        await Expect(talk.Screen.Card).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.CardText).ToHaveTextAsync(ANON);
        await Expect(strip.OnScreen).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(strip.TakeOff).ToBeVisibleAsync();
        await Expect(strip.Error).ToHaveCountAsync(0);
        await Expect(guest.Questions.QuestionOnScreen(2)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await strip.Answer.ClickAsync();

        await Expect(talk.Screen.Card).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(strip.Text).ToHaveTextAsync(NAMED, new() { Timeout = PUSH_TIMEOUT });
        await Expect(strip.Counts).ToContainTextAsync("1 open");
        await Expect(strip.Error).ToHaveCountAsync(0);
        await Expect(guest.Questions.QuestionAnswered(2)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(guest.Questions.QuestionOnScreen(2)).ToHaveCountAsync(0);
    }

    // Sam's deck over MCP: a quote, then a qa slide. Sam opens the presenter window, waits for it to join the
    // presentation, goes live from its chip and opens the audience window beside it. The presenter is on slide 1 when
    // this returns.
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
        return new Talk(presenter, audience, new QaPanel(Page), new QaSlide(audience.Slide(QA)), joinUrl, deckId);
    }

    private async Task AskAsync(Phone phone, string text)
    {
        await phone.Questions.Ask.ClickAsync();
        await phone.Questions.AskSheetText.FillAsync(text);
        await phone.Questions.AskSheetSend.ClickAsync();
        await Expect(phone.Questions.AskSheet).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
    }

    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(guest, new GuestQuestions(page));
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

    // The presenter window, the audience window, the panel in the one and the qa slide in the other, and the join link.
    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, QaPanel Panel, QaSlide Screen, string JoinUrl, string DeckId);

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);
}
