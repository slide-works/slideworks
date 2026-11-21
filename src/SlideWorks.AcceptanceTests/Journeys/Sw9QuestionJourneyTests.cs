using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-9's main journey on the real app, the real hub and the real presentation registry: a question from asking to
// answered. Sam's deck goes in over MCP (a quote, then a qa slide). Sam presents from the presenter window (the test's
// Page, desktop size) with the audience window, the big screen, as a second tab; the first test's guests are browser
// contexts at phone size that open the join link the server gave, and the second test's are API-only guests that join,
// ask and upvote over the guest routes. Every step waits on what the screens show, never on a delay.
public class Sw9QuestionJourneyTests : AcceptanceTest
{
    private const int QUOTE = 1;
    private const int QA = 2;
    private const int SLIDES = 2;
    private const string QUESTION = "How do you split a big change?";

    // The first assertion after a hub push waits as long as the repo's other journeys wait for a pushed frame.
    private const float PUSH_TIMEOUT = 15_000;

    // Criteria: a guest asks anonymously and sees its question with "You" and one vote; another guest sees it, without
    // "You", and upvotes it; the presenter's panel counts 2 votes; Show on screen puts the card over the slide in the
    // audience window and marks the question "On screen now" on both phones; Answered takes the card away, moves the
    // question to the panel's Answered tab and to the phones' answered section.
    [Test]
    public async Task Question_WhenAskedUpvotedShownAndAnswered_ShouldFollowOnEveryScreen()
    {
        var talk = await PresentAsync("Question journey");
        await talk.Presenter.PressAsync("q");
        await Expect(talk.Panel.Root).ToBeVisibleAsync();
        await Expect(talk.Panel.EmptyOpen).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);

        await first.Questions.QuestionsTab.ClickAsync();
        await Expect(first.Questions.QuestionsPanel).ToBeVisibleAsync();
        await first.Questions.Ask.ClickAsync();
        await first.Questions.AskSheetText.FillAsync(QUESTION);
        await Expect(first.Questions.AskSheetSend).ToContainTextAsync("anonymously");
        await first.Questions.AskSheetSend.ClickAsync();

        await Expect(first.Questions.Question(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION);
        await Expect(first.Questions.QuestionMeta(1)).ToContainTextAsync("Anonymous");
        await Expect(first.Questions.QuestionYou(1)).ToBeVisibleAsync();
        await Expect(first.Questions.QuestionVotes(1)).ToHaveTextAsync("1");
        await Expect(first.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(talk.Panel.Row(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.RowText(1)).ToHaveTextAsync(QUESTION);
        await Expect(talk.Panel.RowMeta(1)).ToContainTextAsync("Anonymous");
        await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("1 vote");

        await second.Questions.QuestionsTab.ClickAsync();
        await Expect(second.Questions.Question(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Questions.QuestionText(1)).ToHaveTextAsync(QUESTION);
        await Expect(second.Questions.QuestionYou(1)).ToHaveCountAsync(0);
        await Expect(second.Questions.QuestionVotes(1)).ToHaveTextAsync("1");
        await Expect(second.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "false");

        await second.Questions.QuestionUpvote(1).ClickAsync();

        await Expect(second.Questions.QuestionUpvote(1)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(second.Questions.QuestionVotes(1)).ToHaveTextAsync("2");
        await Expect(talk.Panel.RowVotes(1)).ToContainTextAsync("2 votes", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Questions.QuestionVotes(1)).ToHaveTextAsync("2", new() { Timeout = PUSH_TIMEOUT });

        // Nothing is on the big screen until the presenter shows it.
        await Expect(talk.Screen.Card).ToHaveCountAsync(0);
        await talk.Panel.Show(1).ClickAsync();

        await Expect(talk.Screen.Card).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.CardText).ToHaveTextAsync(QUESTION);
        await Expect(talk.Screen.CardName).ToHaveTextAsync("Anonymous");
        await Expect(talk.Screen.CardVotes).ToContainTextAsync("2");
        await Expect(talk.Audience.Layer(QUOTE)).ToHaveAttributeAsync("data-active", "true");
        await Expect(talk.Panel.RowMeta(1)).ToContainTextAsync("On screen now");
        await Expect(talk.Panel.Unshow(1)).ToBeVisibleAsync();
        await Expect(first.Questions.QuestionOnScreen(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Questions.QuestionOnScreen(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await talk.Panel.Answer(1).ClickAsync();

        await Expect(talk.Screen.Card).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Row(1)).ToHaveCountAsync(0);
        await talk.Panel.TabAnswered.ClickAsync();
        await Expect(talk.Panel.Row(1)).ToBeVisibleAsync();
        await Expect(talk.Panel.Reopen(1)).ToBeVisibleAsync();
        await Expect(talk.Panel.RowMeta(1)).Not.ToContainTextAsync("On screen now");
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Questions.QuestionAnswered(1)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Questions.QuestionOnScreen(1)).ToHaveCountAsync(0);
            await Expect(phone.Questions.QuestionUpvote(1)).ToHaveCountAsync(0);
            await Expect(phone.Questions.ListAnswered).ToBeVisibleAsync();
            await Expect(phone.Questions.ListAnswered.GetByTestId("qa-question-1")).ToBeVisibleAsync();
            await Expect(phone.Questions.QuestionVotes(1)).ToContainTextAsync("2 votes");
        }
    }

    // Criteria: with seven questions of different votes the qa slide lists the top five in order of votes; showing a
    // question swaps the list for that one question at full size, and taking it off brings the list back.
    [Test]
    public async Task Question_WhenOnTheQaSlide_ShouldListTheTopFiveAndSwapForTheShownOne()
    {
        var talk = await PresentAsync("Question top five");
        await talk.Presenter.PressAsync("ArrowRight");
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{QA} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Audience.Layer(QA)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Empty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        // Seven guests; guest g asks question g, then upvotes every question from g up, so question k has k votes.
        var guests = new List<ApiGuest>();
        var contexts = new List<IBrowserContext>();
        for (var g = 1; g <= 7; g++)
        {
            var phone = await OpenPhoneAsync();
            contexts.Add(phone);
            var phoneGuest = await JoinAsync(phone, talk.JoinUrl);
            await Expect(phoneGuest.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{QA}", new() { Timeout = PUSH_TIMEOUT });
            var guest = ApiGuest.On(phone, talk.JoinUrl);
            guests.Add(guest);
            (await guest.AskAsync($"Question {g} of seven")).ShouldBe(g);
        }
        for (var g = 1; g <= 7; g++)
        {
            await guests[g - 1].UpvoteAsync(Enumerable.Range(g, 8 - g).ToArray());
        }

        try
        {
            await Expect(talk.Screen.Row(7)).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Screen.RowVotes(7)).ToContainTextAsync("7", new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Screen.RowVotes(3)).ToContainTextAsync("3", new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Screen.RowText(7)).ToContainTextAsync("Question 7 of seven");
            await Expect(talk.Screen.Row(2)).ToHaveCountAsync(0);
            await Expect(talk.Screen.Row(1)).ToHaveCountAsync(0);
            await Expect(talk.Screen.Total).ToContainTextAsync("7 questions");
            await Expect(talk.Screen.FooterNote).ToContainTextAsync("Top 5 of 7");
            await Expect(talk.Screen.OneUp).ToHaveCountAsync(0);
            (await RowOrderAsync(talk.Screen)).ShouldBe(["7", "6", "5", "4", "3"]);

            await talk.Presenter.PressAsync("q");
            await Expect(talk.Panel.Root).ToBeVisibleAsync();
            await talk.Panel.Show(2).ClickAsync();

            await Expect(talk.Screen.OneUp).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Screen.OneUpText).ToHaveTextAsync("Question 2 of seven");
            await Expect(talk.Screen.OneUpVotes).ToContainTextAsync("2");
            await Expect(talk.Screen.Rows).ToHaveCountAsync(0);
            await Expect(talk.Screen.Card).ToHaveCountAsync(0);

            await talk.Panel.Unshow(2).ClickAsync();

            await Expect(talk.Screen.OneUp).ToHaveCountAsync(0, new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Screen.Rows).ToBeVisibleAsync();
            (await RowOrderAsync(talk.Screen)).ShouldBe(["7", "6", "5", "4", "3"]);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    // The numbers of the list's rows on the qa slide, in the order they are drawn.
    private static Task<string[]> RowOrderAsync(QaSlide screen) =>
        screen.Rows.EvaluateAsync<string[]>(
            "list => [...list.querySelectorAll('[data-testid]')].map(e => e.getAttribute('data-testid')).filter(id => /^qa-slide-row-\\d+$/.test(id)).map(id => id.split('-').pop())");

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
        return new Talk(presenter, audience, new QaPanel(Page), new QaSlide(audience.Slide(QA)), joinUrl);
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
    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, QaPanel Panel, QaSlide Screen, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestQuestions Questions);

    // A guest on a phone's browser context: its page follows the talk, which puts it on the session's roster, and its
    // asking and upvoting go over the guest routes the phone uses, with the same sw_guest cookie. Each write is read
    // back from the server's answer.
    private sealed class ApiGuest
    {
        private readonly IAPIRequestContext _request;
        private readonly string _code;
        private readonly string _origin;

        private ApiGuest(IAPIRequestContext request, string origin, string code)
        {
            _request = request;
            _origin = origin;
            _code = code;
        }

        public static ApiGuest On(IBrowserContext context, string joinUrl)
        {
            // The cookie belongs to the join host, a different origin from the app's own, so the calls go there.
            var url = new Uri(joinUrl);
            var parts = url.AbsolutePath.TrimEnd('/').Split('/');
            return new ApiGuest(context.APIRequest, url.GetLeftPart(UriPartial.Authority), parts[^1]);
        }

        // Asks anonymously; returns the number the server gave the question.
        public async Task<int> AskAsync(string text)
        {
            var response = await _request.PostAsync($"{_origin}/api/join/{_code}/questions", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Origin"] = _origin },
                DataObject = new { text, name = (string?)null },
            });
            response.Status.ShouldBe(201, await response.TextAsync());
            return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("question").GetProperty("number").GetInt32();
        }

        // Upvotes the questions, keeping the guest's existing picks, and checks the server stored them.
        public async Task UpvoteAsync(int[] numbers)
        {
            var read = await _request.GetAsync($"{_origin}/api/join/{_code}/questions");
            read.Status.ShouldBe(200, await read.TextAsync());
            var view = JsonDocument.Parse(await read.TextAsync()).RootElement;
            var round = view.GetProperty("room").GetProperty("round").Clone();
            var mine = view.GetProperty("mine");
            var picks = mine.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).Union(numbers).Order().ToArray();
            var response = await _request.PutAsync($"{_origin}/api/join/{_code}/questions/votes", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Origin"] = _origin },
                DataObject = new { round, picks, revision = mine.GetProperty("revision").Clone() },
            });
            response.Status.ShouldBe(200, await response.TextAsync());
            var stored = JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).Order();
            stored.ShouldBe(picks);
        }
    }
}
