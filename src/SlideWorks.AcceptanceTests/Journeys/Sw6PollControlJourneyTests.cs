using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-6's presenter controls on the real app, the real hub and the real presentation registry (E1b): the poll panel in the
// presenter window, with its buttons and its P, R and A keys, and the poll strip on the phone remote. Sam's deck goes in
// over MCP: a quote, a poll and a quote, with the poll's settings set through update_poll. Sam's presenter window is the
// test's Page and goes live from its own chip; each guest is another browser context at 390×844 on the join code, and
// the phone remote is a third context signed in as Sam at a phone viewport.
//
// The presenter's moves are made only once the window has joined (its Now panel shows) and each move waits for the
// window to show it before the next. The strip ignores taps for 500 ms after any tap and after a slide, round or phase
// change, so each tap on it waits for the phase it acts on to render and then a little over that guard.
public partial class Sw6PollControlJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL_SLIDE = 2;
    private const string QUESTION = "How long does your average merge request stay open?";

    // A little over the strip's 500 ms tap guard.
    private const int STRIP_GUARD_MS = 650;

    private static readonly string[] OPTIONS = ["Under a day", "Two to five days", "Over a week"];

    // D2, D19 and V4: a manual poll stays in draft when the presenter reaches it, also when they leave and come back, and
    // opens only from the panel. Before Go live the panel's Open is refused with a plain sentence, never a code.
    [Test]
    public async Task Poll_WhenSetToManual_ShouldStayNotOpenUntilOpenedFromThePanel()
    {
        var deckId = await CreatePollDeckAsync("Manual poll", new() { ["opens"] = "manual" });
        var presenter = await OpenPresenterAsync(deckId);
        var panel = new PollPanel(Page);

        // Not live yet: the panel shows the poll in draft, round 1, and refuses Open with a sentence that says what to do.
        await MoveAsync(presenter, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Settings).ToHaveTextAsync("Pick one · Live");
        await panel.Open.ClickAsync();
        await Expect(panel.Error).ToHaveTextAsync("The deck isn't live, so the poll can't open. Go live first, then open it.");
        await Expect(panel.State).ToHaveTextAsync("Draft");

        await MoveAsync(presenter, "ArrowLeft", 1);
        var code = await GoLiveAsync(presenter);
        await using var phoneContext = await OpenGuestContextAsync();
        var phone = await JoinAsync(phoneContext, code, presenter, 1);
        var card = new GuestPollCard(phone);

        // Live, the presenter reaches the poll, moves on and comes back: arrival leaves a manual poll alone each time. Each
        // move is one hub call from the window and the hub runs one call per connection at a time, so by the time the
        // window shows slide 3 the arrival on slide 2 has run, and the panel's read on coming back follows it.
        await MoveAsync(presenter, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(card.Question).ToHaveTextAsync(QUESTION);
        await Expect(card.NotOpen).ToBeVisibleAsync();
        await Expect(card.Option(0)).ToBeDisabledAsync();
        await MoveAsync(presenter, "ArrowRight", 3);
        await Expect(card.Root).ToHaveCountAsync(0);
        await MoveAsync(presenter, "ArrowLeft", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Close).ToHaveCountAsync(0);
        await Expect(card.NotOpen).ToBeVisibleAsync();

        // Open from the panel is accepted only from draft, so it opening round 1 without a refusal shows nothing opened it
        // before. The phone can answer now, and the panel counts the answer.
        await panel.Open.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(panel.Open).ToHaveCountAsync(0);
        await Expect(panel.Close).ToBeVisibleAsync();
        await Expect(card.NotOpen).ToHaveCountAsync(0);
        await card.Option(1).CheckAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(panel.Count(0)).ToHaveTextAsync("0");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Count(2)).ToHaveTextAsync("0");
    }

    // D7 and V4: New round after a close starts round 2 from zero, open at once on an on-arrival poll, and the panel lists
    // round 1 with its answers as kept. The phone starts the new round with nothing picked. Once the session ends, the
    // panel starts again from round 1 in draft: earlier rounds belong to their session.
    [Test]
    public async Task Poll_WhenNewRound_ShouldStartFromZeroAndListRoundOne()
    {
        var deckId = await CreatePollDeckAsync("New round", []);
        var presenter = await OpenPresenterAsync(deckId);
        var panel = new PollPanel(Page);
        var code = await GoLiveAsync(presenter);
        await using var phoneContext = await OpenGuestContextAsync();
        var phone = await JoinAsync(phoneContext, code, presenter, 1);
        var card = new GuestPollCard(phone);

        await MoveAsync(presenter, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.History).ToHaveCountAsync(0);
        await card.Option(1).CheckAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Rate).ToHaveTextAsync("100% of 1 joined");

        await panel.Close.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(card.Closed).ToBeVisibleAsync();
        await Expect(panel.ShowAnswer).ToHaveCountAsync(0);

        await panel.NewRound.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 2");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(panel.Answered).ToHaveTextAsync("0 answers");
        await Expect(panel.Count(0)).ToHaveTextAsync("0");
        await Expect(panel.Count(1)).ToHaveTextAsync("0");
        await Expect(panel.Count(2)).ToHaveTextAsync("0");
        await Expect(panel.HistoryRound(1)).ToHaveTextAsync("Round 1: 1 answer, 100% of 1 joined, kept");
        await Expect(panel.HistoryRound(2)).ToHaveCountAsync(0);
        await Expect(card.Round).ToHaveTextAsync("Round 2");
        await Expect(card.Closed).ToHaveCountAsync(0);
        await Expect(card.Option(1)).Not.ToBeCheckedAsync();
        await Expect(card.Option(1)).ToBeEnabledAsync();

        // The new round counts on its own; round 1 stays as it was.
        await card.Option(2).CheckAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(panel.Count(1)).ToHaveTextAsync("0");
        await Expect(panel.Count(2)).ToHaveTextAsync("1");
        await Expect(panel.HistoryRound(1)).ToHaveTextAsync("Round 1: 1 answer, 100% of 1 joined, kept");

        // End closes the open round. Outside a live session the panel reads the poll as draft, round 1, with no earlier
        // rounds: the window is loaded again so the panel reads it afresh.
        await presenter.Live.End.ClickAsync();
        await Expect(presenter.Live.GoLive).ToBeVisibleAsync();
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"{POLL_SLIDE} / {SLIDES}");
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.History).ToHaveCountAsync(0);
    }

    // D6, D19 and V4: with the results hidden until close, R reveals them to the room, P closes the poll and A shows the
    // answer. The panel marks the correct option; the phone that picked it is told it got it, the other is told the answer.
    [Test]
    public async Task Poll_WhenAnswerIsShown_ShouldTellThePhoneItWasRight()
    {
        var deckId = await CreatePollDeckAsync("Show answer", new() { ["results"] = "afterClose", ["correct"] = 2 });
        var presenter = await OpenPresenterAsync(deckId);
        var panel = new PollPanel(Page);
        var code = await GoLiveAsync(presenter);
        await using var rightContext = await OpenGuestContextAsync();
        var right = new GuestPollCard(await JoinAsync(rightContext, code, presenter, 1));
        await using var missContext = await OpenGuestContextAsync();
        var miss = new GuestPollCard(await JoinAsync(missContext, code, presenter, 2));

        await MoveAsync(presenter, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Settings).ToHaveTextAsync("Pick one · After close");
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room until the poll closes.");
        await Expect(panel.Keys).ToHaveTextAsync("P open or close the poll · R reveal · A show answer");
        await right.Option(1).CheckAsync();
        await Expect(right.Sent).ToBeVisibleAsync();
        await miss.Option(0).CheckAsync();
        await Expect(miss.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(panel.Count(0)).ToHaveTextAsync("1");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");

        // Each key waits for the panel to show what the one before did, since the panel ignores keys while a call is out.
        await Expect(panel.Reveal).ToBeVisibleAsync();
        await presenter.PressAsync("r");
        await Expect(panel.Room).ToHaveTextAsync("Revealed: the room sees these results.");
        await Expect(panel.Reveal).ToHaveCountAsync(0);
        await Expect(panel.State).ToHaveTextAsync("Open");

        await presenter.PressAsync("p");
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(panel.Room).ToHaveTextAsync("The room sees the final result.");
        await Expect(right.Closed).ToBeVisibleAsync();
        await Expect(miss.Closed).ToBeVisibleAsync();
        await Expect(right.Verdict).ToHaveCountAsync(0);
        await Expect(panel.Correct(1)).ToHaveCountAsync(0);

        await Expect(panel.ShowAnswer).ToBeVisibleAsync();
        await presenter.PressAsync("a");
        await Expect(panel.Correct(1)).ToHaveTextAsync("✓ Correct");
        await Expect(panel.Correct(0)).ToHaveCountAsync(0);
        await Expect(panel.ShowAnswer).ToHaveCountAsync(0);
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(panel.Error).ToHaveCountAsync(0);

        await Expect(right.VerdictRight).ToHaveTextAsync("You got it.");
        await Expect(right.VerdictMiss).ToHaveCountAsync(0);
        await Expect(right.OptionCorrect(1)).ToBeVisibleAsync();
        await Expect(miss.VerdictMiss).ToHaveTextAsync($"Not this time. The answer was “{OPTIONS[1]}”.");
        await Expect(miss.VerdictRight).ToHaveCountAsync(0);
    }

    // V8: the poll strip on the phone remote. The poll opens on arrival when the phone moves the talk there, the strip
    // counts the answer, and its Close closes the poll on the big screen (the audience window), in the presenter window's
    // panel and on the guest's phone.
    [Test]
    public async Task Poll_WhenClosedFromThePhoneRemote_ShouldCloseOnScreenAndPhone()
    {
        var deckId = await CreatePollDeckAsync("Remote close", []);
        var presenter = await OpenPresenterAsync(deckId);
        var panel = new PollPanel(Page);
        var code = await GoLiveAsync(presenter);

        var audienceTab = await Context.NewPageAsync();
        var audience = new PlayerPage(audienceTab);
        await audienceTab.GotoAsync($"{PlayerPage.PathFor(deckId)}?present");
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        var screen = new PollSlide(audience.Slide(POLL_SLIDE));

        await using var phoneContext = await OpenGuestContextAsync();
        var phone = new GuestPollCard(await JoinAsync(phoneContext, code, presenter, 1));

        await using var remoteContext = await OpenRemoteContextAsync();
        var remotePage = await remoteContext.NewPageAsync();
        await SignIn.AsAsync(remotePage, TestUser.Sam);
        var speaker = new SpeakerPage(remotePage);
        var strip = new PollStrip(remotePage);
        await speaker.GotoAsync(deckId);
        await speaker.IsLiveAsync();
        await Expect(speaker.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(strip.Root).ToHaveCountAsync(0);

        await speaker.Next.ClickAsync();
        await Expect(speaker.Count).ToHaveTextAsync($"{POLL_SLIDE} / {SLIDES}");
        await Expect(presenter.Count).ToHaveTextAsync($"{POLL_SLIDE} / {SLIDES}");
        await Expect(strip.State).ToHaveTextAsync("Open");
        await Expect(strip.Answered).ToHaveTextAsync("0");
        await Expect(strip.Open).ToHaveCountAsync(0);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(panel.State).ToHaveTextAsync("Open");

        await phone.Option(0).CheckAsync();
        await Expect(phone.Sent).ToBeVisibleAsync();
        await Expect(strip.Answered).ToHaveTextAsync("1");
        await Expect(strip.Rate).ToHaveTextAsync("100% of 1");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");

        // One tap, once the open phase has been on screen for longer than the strip's tap guard.
        await remotePage.WaitForTimeoutAsync(STRIP_GUARD_MS);
        await strip.Close.ClickAsync();
        await Expect(strip.State).ToHaveTextAsync("Closed");
        await Expect(strip.Close).ToHaveCountAsync(0);
        await Expect(strip.NewRound).ToBeVisibleAsync();
        await Expect(strip.Error).ToHaveCountAsync(0);
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(panel.Count(0)).ToHaveTextAsync("1");
        await Expect(phone.Closed).ToBeVisibleAsync();
        await Expect(phone.Option(0)).ToBeDisabledAsync();
        await Expect(phone.OptionPercent(0)).ToHaveTextAsync("100%");
    }

    // Sam's deck: a quote, the poll on slide 2 (question and options as its slots) and a quote. settings are update_poll's
    // arguments; none leaves the poll on its defaults (pick one, results live, opens on arrival, no correct answer).
    private static async Task<string> CreatePollDeckAsync(string title, Dictionary<string, object?> settings)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
        var deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "poll", ["slots"] = new { question = QUESTION, options = OPTIONS } });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Then keep going.", attribution = "Sam Rivera" } });
        if (settings.Count > 0)
        {
            await AgentSeed.CallAsync(agent, "update_poll", new(settings) { ["deck"] = deckId, ["slide"] = POLL_SLIDE });
        }
        return deckId;
    }

    // Sam's presenter window on the deck, in the test's own context, joined and on slide 1.
    private async Task<PresenterPage> OpenPresenterAsync(string deckId)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLoadedAsync();
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        return presenter;
    }

    // Go live from the presenter window's chip; returns the join code without its space.
    private async Task<string> GoLiveAsync(PresenterPage presenter)
    {
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.Code).ToHaveTextAsync(CodeLabel());
        await Expect(presenter.Live.Count).ToHaveTextAsync("0 joined");
        return (await presenter.Live.Code.InnerTextAsync()).Replace(" ", "", StringComparison.Ordinal);
    }

    // A guest's phone joins on the code and follows; the presenter's chip counts it.
    private async Task<IPage> JoinAsync(IBrowserContext context, string code, PresenterPage presenter, int joined)
    {
        var page = await context.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoAsync(code);
        await guest.IsFollowingAsync();
        await Expect(presenter.Live.Count).ToHaveTextAsync($"{joined} joined");
        return page;
    }

    // A key in the presenter window, then the window showing the slide it moved to.
    private async Task MoveAsync(PresenterPage presenter, string key, int slide)
    {
        await presenter.PressAsync(key);
        await Expect(presenter.Count).ToHaveTextAsync($"{slide} / {SLIDES}");
    }

    private Task<IBrowserContext> OpenGuestContextAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        BaseURL = AcceptanceApp.BaseUrl,
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    // The phone remote: Sam's own browser context at a phone viewport with touch.
    private Task<IBrowserContext> OpenRemoteContextAsync()
    {
        var options = ContextOptions();
        options.ViewportSize = new ViewportSize { Width = 390, Height = 844 };
        options.IsMobile = true;
        options.HasTouch = true;
        return Browser.NewContextAsync(options);
    }

    [GeneratedRegex(@"^\d{3} \d{3}$")]
    private static partial Regex CodeLabel();
}
