using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-6's presenter controls on the real app, the real hub and the real presentation registry (E1b): the poll panel in the
// presenter window, with its buttons and its P, R and A keys, and the poll strip on the phone remote. Sam's deck goes in
// over MCP: a quote, a poll and a quote, with the poll's settings set through update_poll. Sam's presenter window is the
// test's Page and goes live from its own chip, and the audience window (the room's big screen) is a second tab of the
// same browser. Each guest is another browser context at 390×844 on the join code, and the phone remote is a further
// context signed in as Sam at a phone viewport.
//
// The presenter's moves are made only once the window has joined (its Now panel shows), and each move waits for both
// windows to show it before the next. The panel keeps the last view of each slide it has read, so a check that the panel
// read a slide afresh asserts something only a fresh view can carry (the number of guests joined at the time). Absence on
// the big screen (no counts while hidden or in draft) is checked only once the screen shows a state or a count that
// proves it has caught up, and the same probe is shown to find the counts once they show.
public partial class Sw6PollControlJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL_SLIDE = 2;
    private const string QUESTION = "How long does your average merge request stay open?";

    // Returns everything under the element that could carry a count or a share: any attribute whose value holds a
    // percentage or a width, and any text that holds a percentage. Empty while the counts are not shown.
    private const string COUNT_PROBE =
        """
        root => {
          const found = [];
          const leaks = /\d\s*%|width\s*:/i;
          for (const element of [root, ...root.querySelectorAll('*')]) {
            for (const attribute of element.attributes) {
              if (leaks.test(attribute.value)) found.push(`${element.tagName.toLowerCase()} ${attribute.name}="${attribute.value}"`);
            }
          }
          const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (/\d\s*%/.test(node.textContent)) found.push(`text "${node.textContent.trim()}"`);
          }
          return found;
        }
        """;

    private static readonly string[] OPTIONS = ["Under a day", "Two to five days", "Over a week"];

    // D2, D19 and V4: a manual poll stays in draft when the presenter reaches it, also when they leave and come back, on
    // the panel, the big screen and the phone, and opens only from the panel (here its P key). Before Go live the panel's
    // Open is refused with a plain sentence, never a code.
    [Test]
    public async Task Poll_WhenSetToManual_ShouldStayNotOpenUntilOpenedFromThePanel()
    {
        var deckId = await CreatePollDeckAsync("Manual poll", new() { ["opens"] = "manual" });
        var presenter = await OpenPresenterAsync(deckId);
        var audience = await OpenAudienceAsync(presenter, deckId);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL_SLIDE));

        // Not live yet: the panel's first read of the poll (nobody joined) shows it in draft, round 1, and Open is refused
        // with a sentence that says what to do.
        await MoveAsync(presenter, audience, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Rate).ToHaveTextAsync("0 joined");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Settings).ToHaveTextAsync("Pick one · Live");
        await panel.Open.ClickAsync();
        await Expect(panel.Error).ToHaveTextAsync("The deck isn't live, so the poll can't open. Go live first, then open it.");
        await Expect(panel.State).ToHaveTextAsync("Draft");

        await MoveAsync(presenter, audience, "ArrowLeft", 1);
        var code = await GoLiveAsync(presenter);
        await using var phoneContext = await OpenGuestContextAsync();
        var card = new GuestPollCard(await JoinAsync(phoneContext, code, presenter, 1));

        // Live, the presenter reaches the poll. The panel's view counts the guest who joined since the last read, so it is
        // a fresh one: still draft. The big screen shows the question with empty tracks, and the phone says it isn't open.
        await MoveAsync(presenter, audience, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Rate).ToHaveTextAsync("0% of 1 joined");
        await Expect(screen.State).ToHaveTextAsync("Not open yet");
        await Expect(screen.Question).ToHaveTextAsync(QUESTION);
        foreach (var position in Enumerable.Range(0, OPTIONS.Length))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
        }
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the big screen's rows in draft");
        await Expect(card.Question).ToHaveTextAsync(QUESTION);
        await Expect(card.NotOpen).ToBeVisibleAsync();
        await Expect(card.Option(0)).ToBeDisabledAsync();

        // The presenter moves on, a second guest joins, and the presenter comes back. Each move is one hub call from the
        // window and the hub runs one call per connection at a time, so the arrival on slide 2 had run before the window
        // showed slide 3. A view counting the second guest was made after that arrival: it must still be draft.
        await MoveAsync(presenter, audience, "ArrowRight", 3);
        await Expect(card.Root).ToHaveCountAsync(0);
        await using var secondContext = await OpenGuestContextAsync();
        await JoinAsync(secondContext, code, presenter, 2);
        await MoveAsync(presenter, audience, "ArrowLeft", POLL_SLIDE);
        await Expect(panel.Rate).ToHaveTextAsync("0% of 2 joined");
        await Expect(panel.State).ToHaveTextAsync("Draft");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Close).ToHaveCountAsync(0);
        await Expect(screen.State).ToHaveTextAsync("Not open yet");
        await Expect(card.NotOpen).ToBeVisibleAsync();

        // P opens it. The server accepts Open only from draft, so round 1 opening with no refusal also shows nothing opened
        // it before. The big screen and the phone open, the phone answers, and the counts show (results are live).
        await Expect(panel.Open).ToBeVisibleAsync();
        await presenter.PressAsync("p");
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(panel.Open).ToHaveCountAsync(0);
        await Expect(panel.Close).ToBeVisibleAsync();
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.Answered).ToHaveTextAsync("0 answers");
        await Expect(card.NotOpen).ToHaveCountAsync(0);
        await card.Option(1).CheckAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(panel.Count(0)).ToHaveTextAsync("0");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Count(2)).ToHaveTextAsync("0");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.Percent(1)).ToHaveTextAsync("100%");
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldNotBeEmpty("the probe should find the shares once they show");
    }

    // D7 and V4: New round after a close starts round 2 from zero, open at once on an on-arrival poll, on the panel, the
    // big screen and the phone, and the panel lists round 1 with its answers as kept. Once the session ends, the panel
    // starts again from round 1 in draft: earlier rounds belong to their session.
    [Test]
    public async Task Poll_WhenNewRound_ShouldStartFromZeroAndListRoundOne()
    {
        var deckId = await CreatePollDeckAsync("New round", []);
        var presenter = await OpenPresenterAsync(deckId);
        var audience = await OpenAudienceAsync(presenter, deckId);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL_SLIDE));
        var code = await GoLiveAsync(presenter);
        await using var phoneContext = await OpenGuestContextAsync();
        var card = new GuestPollCard(await JoinAsync(phoneContext, code, presenter, 1));

        await MoveAsync(presenter, audience, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 1");
        await Expect(panel.History).ToHaveCountAsync(0);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.Round).ToHaveCountAsync(0);
        await card.Option(1).CheckAsync();
        await Expect(card.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Rate).ToHaveTextAsync("100% of 1 joined");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(screen.Bar(1)).ToHaveAttributeAsync("style", Width(100));

        await panel.Close.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(card.Closed).ToBeVisibleAsync();
        await Expect(panel.ShowAnswer).ToHaveCountAsync(0);

        // Round 2: zero everywhere, round 1 kept in the panel only. The big screen shows the new round, not round 1's bars.
        await panel.NewRound.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Round).ToHaveTextAsync("round 2");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(panel.Answered).ToHaveTextAsync("0 answers");
        await Expect(panel.HistoryRound(1)).ToHaveTextAsync("Round 1: 1 answer, 100% of 1 joined, kept");
        await Expect(panel.HistoryRound(2)).ToHaveCountAsync(0);
        await Expect(screen.Round).ToHaveTextAsync("Round 2");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.Answered).ToHaveTextAsync("0 answers");
        foreach (var position in Enumerable.Range(0, OPTIONS.Length))
        {
            await Expect(panel.Count(position)).ToHaveTextAsync("0");
            await Expect(screen.Percent(position)).ToHaveTextAsync("0%");
            await Expect(screen.Bar(position)).ToHaveAttributeAsync("style", Width(0));
        }
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
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("100%");

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

    // D6, D19 and V4: with the results hidden until close, A does nothing while the poll is open, R reveals the results
    // to the room, P closes the poll and A shows the answer. The panel and the big screen mark the correct option; the
    // phone that picked it is told it got it, the other is told the answer.
    [Test]
    public async Task Poll_WhenAnswerIsShown_ShouldTellThePhoneItWasRight()
    {
        var deckId = await CreatePollDeckAsync("Show answer", new() { ["results"] = "afterClose", ["correct"] = 2 });
        var presenter = await OpenPresenterAsync(deckId);
        var audience = await OpenAudienceAsync(presenter, deckId);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL_SLIDE));
        var code = await GoLiveAsync(presenter);
        await using var rightContext = await OpenGuestContextAsync();
        var right = new GuestPollCard(await JoinAsync(rightContext, code, presenter, 1));
        await using var missContext = await OpenGuestContextAsync();
        var miss = new GuestPollCard(await JoinAsync(missContext, code, presenter, 2));

        await MoveAsync(presenter, audience, "ArrowRight", POLL_SLIDE);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Settings).ToHaveTextAsync("Pick one · After close");
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room until the poll closes.");
        await Expect(panel.Keys).ToHaveTextAsync("P open or close the poll · R reveal · A show answer");
        await right.Option(1).CheckAsync();
        await Expect(right.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("1 answer");

        // A while the poll is open has nothing to do. A refused call would leave its message in the panel until the next
        // action, so once a later change (the second answer) has rendered, the panel shows no message, no correct mark and
        // the poll still open, and no phone has a verdict.
        await presenter.PressAsync("a");
        await miss.Option(0).CheckAsync();
        await Expect(miss.Sent).ToBeVisibleAsync();
        await Expect(panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(panel.Count(0)).ToHaveTextAsync("1");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(panel.Correct(1)).ToHaveCountAsync(0);
        await Expect(panel.ShowAnswer).ToHaveCountAsync(0);
        await Expect(right.Verdict).ToHaveCountAsync(0);
        await Expect(miss.Verdict).ToHaveCountAsync(0);

        // Hidden from the room: the big screen has caught up with both answers and shows no count, bar or mark.
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, OPTIONS.Length))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
        }
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the big screen's rows while hidden");

        // R: the room sees the results while the poll stays open. Each key waits for the panel to show what the one before
        // did, since the panel ignores keys while a call is out.
        await Expect(panel.Reveal).ToBeVisibleAsync();
        await presenter.PressAsync("r");
        await Expect(panel.Room).ToHaveTextAsync("Revealed: the room sees these results.");
        await Expect(panel.Reveal).ToHaveCountAsync(0);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(screen.Bar(1)).ToHaveAttributeAsync("style", Width(50));
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.Correct(1)).ToHaveCountAsync(0);
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldNotBeEmpty("the probe should find the shares once they show");

        await presenter.PressAsync("p");
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(panel.Room).ToHaveTextAsync("The room sees the final result.");
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(right.Closed).ToBeVisibleAsync();
        await Expect(miss.Closed).ToBeVisibleAsync();
        await Expect(right.Verdict).ToHaveCountAsync(0);
        await Expect(panel.Correct(1)).ToHaveCountAsync(0);
        await Expect(screen.Correct(1)).ToHaveCountAsync(0);

        await Expect(panel.ShowAnswer).ToBeVisibleAsync();
        await presenter.PressAsync("a");
        await Expect(panel.Correct(1)).ToHaveTextAsync("✓ Correct");
        await Expect(panel.Correct(0)).ToHaveCountAsync(0);
        await Expect(panel.ShowAnswer).ToHaveCountAsync(0);
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(panel.Error).ToHaveCountAsync(0);
        await Expect(screen.Correct(1)).ToBeVisibleAsync();
        await Expect(screen.Correct(0)).ToHaveCountAsync(0);
        await Expect(screen.Correct(2)).ToHaveCountAsync(0);

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
        var audience = await OpenAudienceAsync(presenter, deckId);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL_SLIDE));
        var code = await GoLiveAsync(presenter);
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

        await TapCloseUntilClosedAsync(strip);
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

    // Taps the strip's Close until the strip reads Closed. The strip ignores a tap for 500 ms after a phase change and
    // while its call is out, and shows no sign of either, so a tap can be swallowed: it is tried again after a second.
    // A repeat cannot act twice: the strip drops taps while the first call is out, the answer and the Closed view land
    // in the same render, and Close is not shown once closed (Show answer and New round sit in other columns), so the
    // loop taps only while Close is still on screen. A refused second Close would show an error, which the caller
    // asserts is absent.
    private async Task TapCloseUntilClosedAsync(PollStrip strip)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (await strip.Close.CountAsync() > 0)
                {
                    await strip.Close.ClickAsync(new LocatorClickOptions { Timeout = 1_000 });
                }
                await Expect(strip.State).ToHaveTextAsync("Closed", new LocatorAssertionsToHaveTextOptions { Timeout = 1_000 });
                return;
            }
            catch (PlaywrightException) when (attempt < 5)
            {
                // Not closed yet: the tap was swallowed, or Close went from under it. Look again and tap if still open.
            }
        }
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

    // The audience window, the room's big screen: a second tab of Sam's browser following the presentation on slide 1.
    private async Task<PlayerPage> OpenAudienceAsync(PresenterPage presenter, string deckId)
    {
        var tab = await Context.NewPageAsync();
        var audience = new PlayerPage(tab);
        await tab.GotoAsync($"{PlayerPage.PathFor(deckId)}?present");
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        return audience;
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

    // A key in the presenter window, then both windows showing the slide it moved to.
    private async Task MoveAsync(PresenterPage presenter, PlayerPage audience, string key, int slide)
    {
        await presenter.PressAsync(key);
        await Expect(presenter.Count).ToHaveTextAsync($"{slide} / {SLIDES}");
        await Expect(audience.Layer(slide)).ToHaveAttributeAsync("data-active", "true");
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

    // A bar's inline width, as React writes it.
    private static Regex Width(int percent) => new($@"width:\s*{percent}%");

    [GeneratedRegex(@"^\d{3} \d{3}$")]
    private static partial Regex CodeLabel();
}
