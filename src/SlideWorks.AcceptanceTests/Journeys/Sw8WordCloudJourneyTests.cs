using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The word cloud's room journey on the real app, the real hub and the real presentation registry. Sam's deck goes in over
// MCP (a join slide, then a word-cloud slide that opens on arrival). Sam presents from the presenter window (the test's
// Page) with the audience window, the big screen, as a second tab; each guest is its own browser context at phone size.
// Every step waits on what a screen shows or on a frame its own hub connection received, never on a delay.
public class Sw8WordCloudJourneyTests : AcceptanceTest
{
    private const int SLIDES = 2;
    private const int CLOUD = 2;
    private const string QUESTION = "One word for this sprint?";

    // The second guest sends the first guest's word with a typo and a capital: it counts with the first word (SW-606), and
    // the cloud shows the first casing anyone sent (SW-8 D2), so the rule that picked the ordinal least would show the second.
    private const string FIRST_CASING = "ship";
    private const string SECOND_CASING = "Shipp";

    // On ProfanityList in SlideWorks.Core.
    private const string PROFANE = "shit";

    private const string PROFANE_SENTENCE = "That word isn\u2019t allowed here. Try a different one.";

    private const float PUSH_TIMEOUT = 15_000;
    private static readonly TimeSpan FRAME_TIMEOUT = TimeSpan.FromSeconds(15);

    // Resolves after two animation frames: a hub message the page has received has been handled and rendered.
    private const string SETTLE = "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(true))))";

    // Criteria: the cloud opens by itself when the presenter reaches its slide. Two guests send the same word, the second
    // one typo off and in another casing: the big screen shows one word, in the first casing, that its connection received
    // with count 2; the panel
    // lists it with count 2 and each phone shows it in the room's words with count 2. The presenter hides it: the big
    // screen and each phone drop it once their own connection has the hide, and the panel still lists it, marked hidden.
    // A third submit with a profane word shows the refusal on the phone and leaves the count at 2.
    [Test]
    public async Task Journey_WhenTwoGuestsRepeatAWord_ShouldGrowTheCloudUntilHide()
    {
        var talk = await PresentAsync("Word cloud");
        await using var firstPhone = await PollJourney.OpenPhoneContextAsync(Browser);
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await PollJourney.OpenPhoneContextAsync(Browser);
        var second = await JoinAsync(secondPhone, talk.JoinUrl);

        await talk.Presenter.PressAsync("ArrowRight");
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{CLOUD} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Audience.Layer(CLOUD)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });

        await Expect(talk.Panel.Phase).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Guests).ToHaveTextAsync("0 guests submitted");
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{CLOUD}", new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Card.Question).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Card.Submit).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });
        }

        await first.Card.Word(0).FillAsync(FIRST_CASING);
        await first.Card.Submit.ClickAsync();
        await Expect(first.Card.Mine).ToHaveTextAsync([FIRST_CASING]);
        await Expect(first.Card.Word(0)).ToBeFocusedAsync();
        await Expect(talk.Panel.Guests).ToHaveTextAsync("1 guest submitted", new() { Timeout = PUSH_TIMEOUT });
        await second.Card.Word(0).FillAsync(SECOND_CASING);
        await second.Card.Submit.ClickAsync();
        // The server stores a later guest's near miss under the word it matches, in the casing already stored for it, so
        // the phone lists it that way.
        await Expect(second.Card.Mine).ToHaveTextAsync([FIRST_CASING]);

        var twice = talk.ScreenHub.SeenAsync(words => words.Any(w => w.Text == FIRST_CASING && w.Count == 2 && !w.Hidden));
        (await Task.WhenAny(twice, Task.Delay(FRAME_TIMEOUT)) == twice).ShouldBeTrue("The big screen never received the word with count 2.");
        await Expect(talk.Screen.Words).ToHaveTextAsync([FIRST_CASING], new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Guests).ToHaveTextAsync("2 guests submitted", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Entries).ToHaveCountAsync(1);
        await Expect(talk.Panel.Entry(1)).ToHaveTextAsync(new Regex($"^{FIRST_CASING}\\s*2\\s*Hide$"));
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Card.Room).ToHaveTextAsync([FIRST_CASING], new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Card.Room).ToHaveAttributeAsync("data-count", "2");
        }

        // Each surface's own connection gets the frame for the hide before its absence is checked; the frames are
        // watched from before the click, so an earlier frame without the word cannot stand in for it.
        var screenHidden = talk.ScreenHub.NextAsync(words => words.Any(w => w.Text == FIRST_CASING && w.Hidden));
        var phonesDropped = new[] { first, second }.Select(p => p.Hub.NextAsync(words => words.All(w => w.Text != FIRST_CASING))).ToList();
        await talk.Panel.Hide(1).ClickAsync();

        await Expect(talk.Panel.Entry(1)).ToHaveTextAsync(new Regex($"^{FIRST_CASING} · Hidden\\s*2\\s*Restore$"), new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Restore(1)).ToBeVisibleAsync();
        (await Task.WhenAny(screenHidden, Task.Delay(FRAME_TIMEOUT)) == screenHidden).ShouldBeTrue("The big screen never received the hide.");
        (await talk.Audience.AudienceMode.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
        await Expect(talk.Screen.Words).ToHaveCountAsync(0);
        await Expect(talk.Screen.Body).Not.ToContainTextAsync(new Regex(FIRST_CASING, RegexOptions.IgnoreCase));
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        foreach (var (phone, dropped) in new[] { first, second }.Zip(phonesDropped))
        {
            (await Task.WhenAny(dropped, Task.Delay(FRAME_TIMEOUT)) == dropped).ShouldBeTrue("A phone never received the hide.");
            (await phone.Guest.Root.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
            await Expect(phone.Card.Room).ToHaveCountAsync(0);
        }
        await Expect(first.Card.Mine).ToHaveTextAsync([FIRST_CASING]);
        await Expect(second.Card.Mine).ToHaveTextAsync([FIRST_CASING]);

        await first.Card.Word(0).FillAsync(PROFANE);
        await first.Card.Submit.ClickAsync();

        await Expect(first.Card.Error).ToHaveTextAsync(PROFANE_SENTENCE, new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.Mine).ToHaveTextAsync([FIRST_CASING]);
        await Expect(talk.Panel.Entries).ToHaveCountAsync(1);
        await Expect(talk.Panel.Entry(1)).ToHaveTextAsync(new Regex($"^{FIRST_CASING} · Hidden\\s*2\\s*Restore$"));
        await Expect(talk.Screen.Words).ToHaveCountAsync(0);
    }

    // Sam's deck over MCP: a join slide and the word cloud (live results, opens on arrival: the defaults). Sam opens the
    // presenter window, goes live from its chip and opens the audience window beside it, its hub frames watched from the
    // first socket. The presenter is on slide 1 when this returns.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "word-cloud", ["slots"] = new { question = QUESTION } });
        }

        var presenter = await PollJourney.OpenPresenterAsync(Page, deckId);
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var joinRead = await PollJourney.ReadAsync(Page, $"/api/decks/{deckId}/live", "joinUrl");
        joinRead.Status.ShouldBe(200, joinRead.Body);
        var joinUrl = joinRead.Value.ShouldNotBeNull();

        var audienceTab = await Context.NewPageAsync();
        var screenHub = new CloudWatch(audienceTab);
        var audience = await Talks.OpenAudienceAsync(Page, audienceTab, presenter, deckId);
        await Page.BringToFrontAsync();
        return new Talk(presenter, audience, new Panel(Page), new Screen(audience.Slide(CLOUD)), screenHub, joinUrl);
    }

    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var hub = new CloudWatch(page);
        var guest = await Talks.FollowAsync(page, joinUrl);
        return new Phone(guest, new WordCard(page), hub);
    }

    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, Panel Panel, Screen Screen, CloudWatch ScreenHub, string JoinUrl);

    private sealed record Phone(GuestPage Guest, WordCard Card, CloudWatch Hub);

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

    // The word-cloud slide on the big screen (WordCloudLayout).
    private sealed class Screen(ILocator slide)
    {
        public ILocator Question => slide.GetByTestId("word-cloud-headline");

        public ILocator Body => slide.GetByTestId("word-cloud-body");

        public ILocator Words => Body.GetByTestId(new Regex(@"^word-cloud-word-\d+$"));
    }

    // The phone's word card (OPEN_RESPONSE_TEST_IDS in src/openResponses/testIds.ts).
    private sealed class WordCard(IPage page)
    {
        private ILocator Root => page.GetByTestId("open-response-word-card");

        public ILocator Question => Root.GetByTestId("open-response-question");

        public ILocator Word(int slot) => Root.GetByTestId("open-response-input").Nth(slot);

        public ILocator Submit => Root.GetByTestId("open-response-submit");

        public ILocator Mine => Root.GetByTestId("open-response-mine-entry");

        public ILocator Room => Root.GetByTestId("open-response-room-entry");

        public ILocator Error => Root.GetByTestId("open-response-error");
    }

    private sealed record Word(string Text, int Count, bool Hidden);

    // Watches the wordCloud events a page's hub WebSockets receive (SignalR's JSON protocol: records split by 0x1E, an
    // invocation of "audienceEvent" whose argument has type "wordCloud"). Playwright raises frame events off the test
    // thread, so the frames seen and the waiters are kept under a lock.
    private sealed class CloudWatch
    {
        private readonly object _gate = new();
        private readonly List<IReadOnlyList<Word>> _seen = [];
        private readonly List<(Func<IReadOnlyList<Word>, bool> Match, TaskCompletionSource Done)> _waiters = [];

        public CloudWatch(IPage page) => page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => Read(frame.Text);

        // Completes once a wordCloud event whose words match has reached the page, including one that already has.
        public Task SeenAsync(Func<IReadOnlyList<Word>, bool> match)
        {
            lock (_gate)
            {
                return _seen.Any(match) ? Task.CompletedTask : Wait(match);
            }
        }

        // Completes once a wordCloud event whose words match reaches the page after this call.
        public Task NextAsync(Func<IReadOnlyList<Word>, bool> match)
        {
            lock (_gate)
            {
                return Wait(match);
            }
        }

        private Task Wait(Func<IReadOnlyList<Word>, bool> match)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((match, done));
            return done.Task;
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
                        && type.GetString() == "wordCloud" && argument.TryGetProperty("words", out var words))
                    {
                        Saw([.. words.EnumerateArray().Select(w => new Word(
                            w.GetProperty("text").GetString()!, w.GetProperty("count").GetInt32(), w.GetProperty("hidden").GetBoolean()))]);
                    }
                }
            }
        }

        private void Saw(IReadOnlyList<Word> words)
        {
            lock (_gate)
            {
                _seen.Add(words);
                foreach (var waiter in _waiters.Where(w => w.Match(words)).ToList())
                {
                    waiter.Done.TrySetResult();
                    _waiters.Remove(waiter);
                }
            }
        }
    }
}
