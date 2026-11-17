using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The live poll's main journey on the real app, the real hub and the real presentation registry: a poll from the
// presenter's arrival to the result. Sam's deck goes in over MCP (a join slide, a pick-one poll with live results that
// opens on arrival, a quote). Sam presents from the presenter window (the test's Page, desktop size) with the audience
// window, the big screen, as a second tab; each guest is its own browser context at phone size that opens the join link
// the server gave. Every step waits on what the screens show, never on a delay: the phone card keeps one answer in
// flight and the panel one call, so each tap waits for the phone to show it before the next. The poll's question and
// options carry no digit, so any digit on a phone's card is a count or a share.
public class Sw6PollJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const string QUESTION = "How big was your last change?";

    // The first assertion after a hub push waits as long as the repo's other journeys wait for a pushed frame
    // (PhoneRemoteRecoveryJourneyTests' and HeldNegotiate's 15 s), since several browser contexts share a loaded host.
    private const float PUSH_TIMEOUT = 15_000;
    private static readonly TimeSpan FRAME_TIMEOUT = TimeSpan.FromSeconds(15);

    // Returns everything on a phone's poll card that could carry a count or a share: a bar, percentage or answer-count
    // element by its test id, any digit in the card's text, a percentage, width, number-valued custom property, aria value
    // or progress role in any attribute, and any data- attribute but data-testid that holds a digit. Empty while the card
    // may not show counts.
    private const string COUNT_PROBE =
        """
        card => {
          const found = [];
          const leaks = /\d\s*%|width\s*:|--[\w-]+\s*:[^;]*\d/i;
          for (const element of [card, ...card.querySelectorAll('*')]) {
            const id = element.getAttribute('data-testid') ?? '';
            if (/^poll-option-(bar|percent)-|^poll-foot-answered$/.test(id)) found.push(`element ${id}`);
            for (const attribute of element.attributes) {
              const { name, value } = attribute;
              if (leaks.test(value)) found.push(`${element.tagName.toLowerCase()} ${name}="${value}"`);
              else if (name === 'aria-valuenow' || name === 'aria-valuetext' || (name === 'role' && value === 'progressbar'))
                found.push(`${element.tagName.toLowerCase()} ${name}="${value}"`);
              else if (name.startsWith('data-') && name !== 'data-testid' && /\d/.test(value))
                found.push(`${element.tagName.toLowerCase()} ${name}="${value}"`);
            }
          }
          const walker = document.createTreeWalker(card, NodeFilter.SHOW_TEXT);
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (/\d/.test(node.textContent)) found.push(`text "${node.textContent.trim()}"`);
          }
          return found;
        }
        """;

    // Resolves after two animation frames: a hub message the page has received has been handled and rendered.
    private const string SETTLE = "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(true))))";

    // Criteria: the poll opens by itself when the presenter reaches its slide, on the panel, the big screen and every phone;
    // a tap sends the answer, and the phone marks it as the guest's own and shows the count once its guest has answered;
    // the big screen and the panel count it. A phone whose guest hasn't answered shows no count, bar or share anywhere on
    // its card while the poll is open, even once the room's tally has reached it; it shows them as soon as it answers.
    [Test]
    public async Task Poll_WhenPresenterArrives_ShouldOpenOnThePhoneAndCountTheAnswer()
    {
        var talk = await PresentAsync("Poll arrival");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Guest.ActivityWaiting).ToBeVisibleAsync();
            await Expect(phone.Card.Root).ToHaveCountAsync(0);
        }

        await MoveToAsync(talk, "ArrowRight", POLL);

        await Expect(talk.Panel.State).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.State).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Answered).ToHaveTextAsync("0 answers");
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        await Expect(first.Card.Root).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{POLL}");
        await Expect(first.Card.Kind).ToHaveTextAsync("Poll · Pick one");
        await Expect(first.Card.Question).ToHaveTextAsync(QUESTION);
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await Expect(first.Card.Option(0)).ToBeEnabledAsync();
        await Expect(second.Card.Option(2)).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });

        await first.Card.Option(1).ClickAsync();

        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.Option(1)).ToBeCheckedAsync();
        await Expect(first.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(first.Card.ChangeHint).ToBeVisibleAsync();
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.OptionPercent(1)).ToHaveTextAsync("100%");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("0");

        // The second guest hasn't answered. Its own hub connection has received the room's tally for the first answer
        // (with counts, since the room sees them) and the page has rendered after it, yet its card shows no count, bar or
        // share anywhere, and nothing is picked on it.
        var caughtUp = second.Hub.TallyAsync(answered: 1);
        (await Task.WhenAny(caughtUp, Task.Delay(FRAME_TIMEOUT)) == caughtUp).ShouldBeTrue("The unanswered phone never received the tally for the first answer.");
        (await second.Guest.Root.Page.EvaluateAsync<bool>(SETTLE)).ShouldBeTrue();
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveCountAsync(0);
        await Expect(second.Card.Sent).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(second.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionPercent(position)).ToHaveCountAsync(0);
            await Expect(second.Card.Option(position)).Not.ToBeCheckedAsync();
        }
        (await second.Card.Root.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the unanswered phone's card while the poll is open");

        // Once it answers, the same card shows the counts, and the probe finds them.
        await second.Card.Option(0).ClickAsync();
        await Expect(second.Card.Sent).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveTextAsync("2 answers so far", new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Card.OptionPercent(0)).ToHaveTextAsync("50%");
        (await second.Card.Root.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldNotBeEmpty("the probe should find the counts once the guest has answered");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers", new() { Timeout = PUSH_TIMEOUT });
    }

    // Criteria: tapping another option while the poll is open changes the guest's answer: the bar moves from the old
    // option to the new one on the big screen, the panel and the phone, and the answer count stays the same.
    [Test]
    public async Task Poll_WhenGuestTapsAnother_ShouldMoveTheBar()
    {
        var talk = await PresentAsync("Poll change");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.Option(0)).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Card.Option(1)).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });

        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await second.Card.Option(1).ClickAsync();
        await Expect(second.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("50%", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.Sent).ToBeVisibleAsync();

        await first.Card.Option(2).ClickAsync();

        await Expect(first.Card.OptionYou(2)).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(first.Card.Option(2)).ToBeCheckedAsync();
        await Expect(first.Card.Option(0)).Not.ToBeCheckedAsync();
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("0%", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("0%", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.OptionPercent(2)).ToHaveTextAsync("50%");
        await Expect(first.Card.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(second.Card.OptionPercent(2)).ToHaveTextAsync("50%", new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Card.OptionYou(1)).ToBeVisibleAsync();
    }

    // Criteria: moving on closes the poll, and the phone shows the next slide with nothing to answer, since the closed
    // result stays on the phone only until the presenter moves on. Coming back shows it closed, not reopened, since a
    // poll opens on arrival only once per session: still round 1, with New round offered and no Close. The closed poll
    // shows its result on every phone: the guest who answered sees the percentages with its own pick marked, the guest
    // who joined before and didn't answer sees them too and is told so, and a guest who joins after the close is told
    // it joined late. Nothing can be tapped any more.
    [Test]
    public async Task Poll_WhenPresenterMovesOnThenBack_ShouldHaveClosedItAndShowTheResultOnThePhone()
    {
        var talk = await PresentAsync("Poll close");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.Option(0)).ToBeEnabledAsync(new() { Timeout = PUSH_TIMEOUT });
        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer", new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Card.TapHint).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });

        await MoveToAsync(talk, "ArrowRight", SLIDES);

        await Expect(first.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Guest.ActivityWaiting).ToBeVisibleAsync();
        await Expect(first.Card.Root).ToHaveCountAsync(0);
        await Expect(second.Guest.ActivityWaiting).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Empty).ToHaveCountAsync(1);
        await Expect(talk.Panel.State).ToHaveCountAsync(0);

        await MoveToAsync(talk, "ArrowLeft", POLL);

        await Expect(talk.Panel.State).ToHaveTextAsync("Closed", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Panel.Round).ToHaveTextAsync("round 1");
        await Expect(talk.Panel.NewRound).ToBeVisibleAsync();
        await Expect(talk.Panel.Close).ToHaveCountAsync(0);
        await Expect(talk.Panel.Open).ToHaveCountAsync(0);
        await Expect(talk.Panel.History).ToHaveCountAsync(0);
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("1");
        await Expect(talk.Screen.State).ToHaveTextAsync("Closed", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Screen.Round).ToHaveCountAsync(0);
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(first.Card.Closed).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(first.Card.Closed).ToContainTextAsync("Poll closed");
        await Expect(first.Card.Closed).ToContainTextAsync("1 answer");
        await Expect(first.Card.Round).ToHaveCountAsync(0);
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.Card.OptionPercent(1)).ToHaveTextAsync("0%");
        await Expect(first.Card.Option(1)).ToBeDisabledAsync();
        await Expect(first.Card.Sent).ToHaveCountAsync(0);
        await Expect(first.Card.ChangeHint).ToHaveCountAsync(0);
        await Expect(second.Card.Closed).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(second.Card.NoAnswer).ToBeVisibleAsync();
        await Expect(second.Card.JoinedLate).ToHaveCountAsync(0);
        await Expect(second.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(second.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(second.Card.Option(0)).ToBeDisabledAsync();

        await using var latePhone = await OpenPhoneAsync();
        var late = await JoinAsync(latePhone, talk.JoinUrl);
        await Expect(late.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{POLL}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(late.Card.Closed).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(late.Card.JoinedLate).ToBeVisibleAsync();
        await Expect(late.Card.NoAnswer).ToHaveCountAsync(0);
        await Expect(late.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(late.Card.Option(2)).ToBeDisabledAsync();
        await Expect(talk.Panel.State).ToHaveTextAsync("Closed");
    }

    // Sam's deck over MCP: a join slide, the poll (pick one, live results, opens on arrival: the defaults) and a quote.
    // Sam opens the presenter window, waits for it to join the presentation, goes live from its chip and opens the
    // audience window beside it. The presenter is on slide 1 when this returns.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new()
            {
                ["deck"] = deckId,
                ["layout"] = "poll",
                ["slots"] = new { question = QUESTION, options = new[] { "A few lines", "A few files", "A whole feature" } },
            });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
        }

        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        // The window can be driven once its join has been answered: the Now panel shows and Connecting has gone.
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
        return new Talk(presenter, audience, new PollPanel(Page), new PollSlide(audience.Slide(POLL)), joinUrl);
    }

    // Presses a key in the presenter window and waits until the presenter window and the audience window both show slide
    // {to}, so the next step starts from the move the server answered.
    private async Task MoveToAsync(Talk talk, string key, int to)
    {
        await talk.Presenter.PressAsync(key);
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{to} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        await Expect(talk.Audience.Layer(to)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });
    }

    // A guest scans the code: the phone follows the presenter's slide. Its hub frames are watched from the first socket.
    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var hub = new TallyWatch(page);
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(guest, new GuestPollCard(page), hub);
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

    // The presenter window, the audience window, the panel in the one and the poll slide in the other, and the join link.
    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, PollPanel Panel, PollSlide Screen, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestPollCard Card, TallyWatch Hub);

    // Watches the pollTally events a page's hub WebSockets receive (SignalR's JSON protocol: records split by 0x1E, an
    // invocation of "audienceEvent" whose argument has type "pollTally"). Playwright raises frame events off the test
    // thread, so the answered counts seen and the waiters are kept under a lock.
    private sealed class TallyWatch
    {
        private readonly object _gate = new();
        private readonly HashSet<int> _seen = [];
        private readonly List<(int Answered, TaskCompletionSource Done)> _waiters = [];

        public TallyWatch(IPage page) => page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => Read(frame.Text);

        // Completes once a pollTally with this answered count has reached the page, including one that already has.
        public Task TallyAsync(int answered)
        {
            lock (_gate)
            {
                if (_seen.Contains(answered))
                {
                    return Task.CompletedTask;
                }
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((answered, done));
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
                        && type.GetString() == "pollTally" && argument.TryGetProperty("answered", out var answered))
                    {
                        Saw(answered.GetInt32());
                    }
                }
            }
        }

        private void Saw(int answered)
        {
            lock (_gate)
            {
                _seen.Add(answered);
                foreach (var waiter in _waiters.Where(w => w.Answered == answered).ToList())
                {
                    waiter.Done.TrySetResult();
                    _waiters.Remove(waiter);
                }
            }
        }
    }
}
