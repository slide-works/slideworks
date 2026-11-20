using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The open-text wall's room journey on the real app, the real hub and the real presentation registry. Sam's deck goes in
// over MCP (a join slide, then an open-text slide that opens on arrival). Sam presents from the presenter window (the
// test's Page) with the audience window, the big screen, as a second tab; each guest is its own browser context at phone
// size. Every step waits on what a screen shows or on a frame its own hub connection received, never on a delay.
public class Sw8OpenTextJourneyTests : AcceptanceTest
{
    private const int SLIDES = 2;
    private const int WALL = 2;
    private const string QUESTION = "What should we stop doing?";
    private const string ANSWER = "Long meetings with no agenda.";

    // One UTF-16 code unit over the 200 an answer may hold (SW-8 D3).
    private static readonly string TooLong = new('x', 201);

    private const float PUSH_TIMEOUT = 15_000;
    private static readonly TimeSpan FRAME_TIMEOUT = TimeSpan.FromSeconds(15);

    // Resolves after two animation frames: a hub message the page has received has been handled and rendered.
    private const string SETTLE = "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(true))))";

    // Criteria: the wall opens by itself when the presenter reaches its slide. One guest sends a sentence: the big screen's
    // wall shows it, the panel lists it and the other phone shows it in the room's answers. The presenter hides it: the
    // big screen and the other phone drop it once their own connection has the hide, the panel still lists it, marked
    // hidden, and the guest who sent it still sees its own answer. Filling the other phone's box with 201 characters
    // leaves 200 in it (SW-8 D3), with no error sentence, and the wall gains no row.
    [Test]
    public async Task Journey_WhenAGuestSendsOpenText_ShouldShowTheWallUntilHide()
    {
        var talk = await PresentAsync("Open text");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);

        await talk.Presenter.PressAsync("ArrowRight");
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{WALL} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Audience.Layer(WALL)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });

        await Expect(talk.Panel.Phase).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Guests).ToHaveTextAsync("0 guests submitted");
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{WALL}", new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Card.Question).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Card.Submit).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });
        }

        await first.Card.Box.FillAsync(ANSWER);
        await first.Card.Submit.ClickAsync();

        await Expect(first.Card.Mine).ToHaveTextAsync(ANSWER);
        await Expect(first.Card.Box).ToBeFocusedAsync();
        await Expect(talk.Screen.Answers).ToHaveCountAsync(1, new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Text).ToHaveTextAsync([ANSWER]);
        await Expect(talk.Panel.Guests).ToHaveTextAsync("1 guest submitted", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Entries).ToHaveCountAsync(1);
        await Expect(talk.Panel.Entry(1)).ToHaveTextAsync(new Regex($"^{Regex.Escape(ANSWER)}\\s*Hide$"));
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Card.Room).ToHaveTextAsync([ANSWER], new() { Timeout = PUSH_TIMEOUT });
        }

        // Each surface's own connection gets the frame for the hide before its absence is checked; the frames are
        // watched from before the click, so an earlier frame without the answer cannot stand in for it.
        var screenHidden = talk.ScreenHub.NextAsync(answers => answers.Any(a => a.Text == ANSWER && a.Hidden));
        var phonesDropped = new[] { first, second }.Select(p => p.Hub.NextAsync(answers => answers.All(a => a.Text != ANSWER))).ToList();
        await talk.Panel.Hide(1).ClickAsync();

        await Expect(talk.Panel.Entry(1)).ToHaveTextAsync(new Regex($"^{Regex.Escape(ANSWER)} · Hidden\\s*Restore$"), new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Restore(1)).ToBeVisibleAsync();
        (await Task.WhenAny(screenHidden, Task.Delay(FRAME_TIMEOUT)) == screenHidden).ShouldBeTrue("The big screen never received the hide.");
        (await talk.Audience.AudienceMode.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
        await Expect(talk.Screen.Answers).ToHaveCountAsync(0);
        await Expect(talk.Screen.Body).Not.ToContainTextAsync(ANSWER);
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        foreach (var (phone, dropped) in new[] { first, second }.Zip(phonesDropped))
        {
            (await Task.WhenAny(dropped, Task.Delay(FRAME_TIMEOUT)) == dropped).ShouldBeTrue("A phone never received the hide.");
            (await phone.Guest.Root.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
            await Expect(phone.Card.Room).ToHaveCountAsync(0);
        }
        await Expect(first.Card.Mine).ToHaveTextAsync(ANSWER);

        // The box counts as D3 does and stops at 200, so the 201st character never reaches it and there is nothing to send.
        await second.Card.Box.FillAsync(TooLong);

        await Expect(second.Card.Box).ToHaveValueAsync(TooLong[..200]);
        (await second.Guest.Root.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
        await Expect(second.Card.Error).ToHaveCountAsync(0);
        await Expect(second.Card.Mine).ToHaveCountAsync(0);
        await Expect(talk.Panel.Entries).ToHaveCountAsync(1);
        (await talk.Audience.AudienceMode.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
        await Expect(talk.Screen.Answers).ToHaveCountAsync(0);
    }

    // Sam's deck over MCP: a join slide and the open-text wall (live results, opens on arrival: the defaults). Sam opens
    // the presenter window, goes live from its chip and opens the audience window beside it, its hub frames watched from
    // the first socket. The presenter is on slide 1 when this returns.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "open-text", ["slots"] = new { question = QUESTION } });
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
        var screenHub = new WallWatch(audienceTab);
        var audience = new PlayerPage(audienceTab);
        await audienceTab.GotoAsync(await ReadAsync($"/api/decks/{deckId}/presenter", "audienceUrl"));
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Page.BringToFrontAsync();
        return new Talk(presenter, audience, new Panel(Page), new Screen(audience.Slide(WALL)), screenHub, joinUrl);
    }

    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var hub = new WallWatch(page);
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(guest, new AnswerCard(page), hub);
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

    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, Panel Panel, Screen Screen, WallWatch ScreenHub, string JoinUrl);

    private sealed record Phone(GuestPage Guest, AnswerCard Card, WallWatch Hub);

    // The presenter's open-response panel (OPEN_PRESENTER_IDS in src/openResponses/presenter/testIds.ts).
    private sealed class Panel(IPage page)
    {
        private ILocator Root => page.GetByTestId("open-response-panel");

        public ILocator Phase => Root.GetByTestId("open-response-phase");

        public ILocator Guests => Root.GetByTestId("open-response-guests");

        public ILocator Entries => Root.GetByTestId(new Regex(@"^open-response-entry-\d+$"));

        public ILocator Entry(int number) => Root.GetByTestId($"open-response-entry-{number}");

        public ILocator Hide(int number) => Root.GetByTestId($"open-response-hide-{number}");

        public ILocator Restore(int number) => Root.GetByTestId($"open-response-restore-{number}");
    }

    // The open-text slide on the big screen (OpenTextLayout).
    private sealed class Screen(ILocator slide)
    {
        public ILocator Question => slide.GetByTestId("open-text-headline");

        public ILocator Body => slide.GetByTestId("open-text-body");

        public ILocator Answers => Body.GetByTestId("open-text-answer");

        public ILocator Text => Body.GetByTestId("open-text-text");
    }

    // The phone's answer card (OPEN_RESPONSE_TEST_IDS in src/openResponses/testIds.ts).
    private sealed class AnswerCard(IPage page)
    {
        private ILocator Root => page.GetByTestId("open-response-answer-card");

        public ILocator Question => Root.GetByTestId("open-response-question");

        public ILocator Box => Root.GetByTestId("open-response-input");

        public ILocator Submit => Root.GetByTestId("open-response-submit");

        public ILocator Mine => Root.GetByTestId("open-response-mine");

        public ILocator Room => Root.GetByTestId("open-response-room-entry");

        public ILocator Error => Root.GetByTestId("open-response-error");
    }

    private sealed record Answer(string Text, bool Hidden);

    // Watches the openText events a page's hub WebSockets receive (SignalR's JSON protocol: records split by 0x1E, an
    // invocation of "audienceEvent" whose argument has type "openText"). Playwright raises frame events off the test
    // thread, so the waiters are kept under a lock.
    private sealed class WallWatch
    {
        private readonly object _gate = new();
        private readonly List<(Func<IReadOnlyList<Answer>, bool> Match, TaskCompletionSource Done)> _waiters = [];

        public WallWatch(IPage page) => page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => Read(frame.Text);

        // Completes once an openText event whose answers match reaches the page after this call.
        public Task NextAsync(Func<IReadOnlyList<Answer>, bool> match)
        {
            lock (_gate)
            {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((match, done));
                return done.Task;
            }
        }

        private void Read(string? text)
        {
            foreach (var record in (text ?? "").Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var message = JsonDocument.Parse(record);
                var root = message.RootElement;
                if (!root.TryGetProperty("target", out var target) || target.GetString() != "audienceEvent"
                    || !root.TryGetProperty("arguments", out var arguments))
                {
                    continue;
                }
                foreach (var argument in arguments.EnumerateArray())
                {
                    if (argument.ValueKind == JsonValueKind.Object && argument.TryGetProperty("type", out var type)
                        && type.GetString() == "openText" && argument.TryGetProperty("answers", out var answers))
                    {
                        Saw([.. answers.EnumerateArray().Select(a => new Answer(a.GetProperty("text").GetString()!, a.GetProperty("hidden").GetBoolean()))]);
                    }
                }
            }
        }

        private void Saw(IReadOnlyList<Answer> answers)
        {
            lock (_gate)
            {
                foreach (var waiter in _waiters.Where(w => w.Match(answers)).ToList())
                {
                    waiter.Done.TrySetResult();
                    _waiters.Remove(waiter);
                }
            }
        }
    }
}
