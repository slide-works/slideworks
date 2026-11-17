using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-6's results settings end to end (E1c): who sees the bars, and when, on the real app, the real hub and the real
// presentation registry. Sam's deck goes in over MCP (a join slide, a poll slide with three options, a quote) and its poll
// settings through update_poll before the presenter reaches the slide, since update_poll is refused while a round is open.
// Sam's presenter window is the test's Page and drives the talk; the audience window, the big screen, is a second tab in
// the same browser; each guest is a browser context of its own at 390×844 that opens the join link the server gave.
//
// A journey sees only the rendered page, so "hidden means hidden" is checked on the DOM with COUNT_PROBE over the whole
// poll card on a phone and the whole poll slide on the big screen. An absence check proves nothing until the phone has
// caught up, so before probing a phone that may not show counts the test waits for a pollTally carrying the latest answer
// count to arrive on that phone's hub socket, then for two animation frames so React has rendered it; the same frames
// are checked to carry no counts while the phone's guest may not see them. The phone shows "N answers so far" only once
// its own guest has answered (PollFoot), the same before the bars are revealed as after (D21 holds on the wire; the
// phone keeps the card clean while choosing, as the mockup's phone states have it). The correct answer is marked nowhere
// until the presenter shows it.
public partial class Sw6PollResultsJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const string QUESTION = "Which release train do we board?";

    // Returns everything under the element that could carry a count or a share, outside what the argument allows:
    //   - any attribute whose value holds a percentage or a CSS width;
    //   - aria-valuenow/valuetext/valuemin/valuemax, title, value and any data-* attribute but data-testid, holding a digit;
    //   - an inline custom property (--name: …) holding a digit;
    //   - any text holding a digit.
    // arg.skip lists selectors whose subtrees are left out; arg.allow is a pattern a whole trimmed text may match.
    private const string COUNT_PROBE =
        """
        (root, arg) => {
          const found = [];
          const skipped = (element) => element !== null && arg.skip.some((selector) => element.closest(selector) !== null);
          const allowed = new RegExp(arg.allow);
          const share = /\d\s*%|width\s*:/i;
          const numeric = /^(aria-valuenow|aria-valuetext|aria-valuemin|aria-valuemax|title|value)$|^data-(?!testid$)/;
          for (const element of [root, ...root.querySelectorAll('*')]) {
            if (skipped(element)) continue;
            for (const attribute of element.attributes) {
              const value = attribute.value;
              if (share.test(value)
                || (numeric.test(attribute.name) && /\d/.test(value))
                || (attribute.name === 'style' && /--[\w-]+\s*:[^;]*\d/.test(value))) {
                found.push(`${element.tagName.toLowerCase()} ${attribute.name}="${value}"`);
              }
            }
          }
          const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
          for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            const text = node.textContent.trim();
            if (/\d/.test(text) && !skipped(node.parentElement) && !allowed.test(text)) found.push(`text "${text}"`);
          }
          return found;
        }
        """;

    // Resolves after the next two paints, so state set by a message already received has been rendered.
    private const string NEXT_PAINT = "() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve(true))))";

    // On a phone the only number allowed while counts are hidden is D21's answer count, "N answers so far", in the foot
    // (poll-foot-answered) and in its polite live region, which repeats the same sentence.
    private static readonly object PHONE_ALLOWS = new { skip = Array.Empty<string>(), allow = @"^\d+ answers? so far$" };

    // On the big screen: D21's answer count ("2 answers", poll-slide-answered) and the join pill (poll-slide-join), whose
    // digits are the join code. Nothing else on the slide may hold a number.
    private static readonly object SCREEN_ALLOWS = new { skip = new[] { "[data-testid=\"poll-slide-answered\"]", "[data-testid=\"poll-slide-join\"]" }, allow = "^$" };

    // After the poll closes: while open, the big screen hatches empty tracks and counts the answers, and a phone that
    // answered confirms it and says when results show; a phone still choosing and a guest who joins mid-round see the
    // open poll with no count, bar or percentage; only the presenter's panel has the counts. Close shows the final result
    // on the big screen and every phone, tells the guest who never answered so, and a guest who joins after the close
    // that it joined late; Show answer then marks the correct option everywhere and tells each phone that answered
    // whether its guest got it.
    [Test]
    public async Task Poll_WhenResultsAfterClose_ShouldHideBarsOnScreenAndPhoneUntilClosed()
    {
        var deck = await CreateDeckAsync("After close", results: "afterClose", correct: 2);
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room until the poll closes.");
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.TapHint).ToBeVisibleAsync();

        // The first guest picks the correct option. Its phone confirms and says when results show.
        await first.Card.Option(1).ClickAsync();
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(first.Card.ResultsLater).ToBeVisibleAsync();
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");

        // The second phone, still choosing, has received the tally for that answer and shows nothing of it.
        (await ReachedAsync(second, Tally(answered: 1))).ShouldBeTrue("the second phone should receive the tally for the first answer");
        await second.Page.EvaluateAsync<bool>(NEXT_PAINT);
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(second.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionPercent(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }
        (await PhoneCountsAsync(second)).ShouldBeEmpty("the phone still choosing, after the first answer");

        // A third guest joins mid-round: it sees the open poll, ready to tap, with nothing of the counts.
        await using var thirdPhone = await OpenPhoneAsync();
        var third = await JoinAsync(thirdPhone, joinUrl);
        await Expect(third.Card.Question).ToHaveTextAsync(QUESTION);
        await Expect(third.Card.TapHint).ToBeVisibleAsync();
        await Expect(third.Card.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(third.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionPercent(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }
        (await PhoneCountsAsync(third)).ShouldBeEmpty("the guest who joined mid-round");

        // The second guest picks another option.
        await second.Card.Option(0).ClickAsync();
        await Expect(second.Card.Sent).ToBeVisibleAsync();
        await Expect(second.Card.ResultsLater).ToBeVisibleAsync();
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(first.Card.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(second.Card.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(panel.Count(0)).ToHaveTextAsync("1");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Count(2)).ToHaveTextAsync("0");
        (await ReachedAsync(third, Tally(answered: 2))).ShouldBeTrue("the mid-round guest should receive the tally for the second answer");
        await third.Page.EvaluateAsync<bool>(NEXT_PAINT);

        // Hidden means hidden: nothing of the counts is on the big screen or any phone, and the correct option is not
        // marked anywhere. No tally with counts reached any phone.
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            foreach (var phone in new[] { first, second, third })
            {
                await Expect(phone.Card.OptionBar(position)).ToHaveCountAsync(0);
                await Expect(phone.Card.OptionPercent(position)).ToHaveCountAsync(0);
                await Expect(phone.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            }
        }
        await Expect(third.Card.Answered).ToHaveCountAsync(0);
        (await ScreenCountsAsync(screen)).ShouldBeEmpty("the big screen while hidden");
        (await PhoneCountsAsync(first)).ShouldBeEmpty("the first phone while hidden");
        (await PhoneCountsAsync(second)).ShouldBeEmpty("the second phone while hidden");
        (await PhoneCountsAsync(third)).ShouldBeEmpty("the mid-round guest's phone while hidden");
        foreach (var phone in new[] { first, second, third })
        {
            TalliesWithCounts(phone).ShouldBeEmpty("no tally with counts should reach a phone while results are hidden");
        }

        // Close: the final result on the big screen and on every phone; the guest who never answered is told so.
        await panel.Close.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(screen.Bar(0)).ToHaveAttributeAsync("style", Width(50));
        (await ScreenCountsAsync(screen)).ShouldNotBeEmpty("the probe should find the shares on the big screen once they show");
        foreach (var phone in new[] { first, second, third })
        {
            await Expect(phone.Card.Closed).ToContainTextAsync("2 answers");
            await Expect(phone.Card.ResultsLater).ToHaveCountAsync(0);
            await Expect(phone.Card.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.Card.OptionPercent(1)).ToHaveTextAsync("50%");
            await Expect(phone.Card.OptionPercent(2)).ToHaveTextAsync("0%");
            await Expect(phone.Card.OptionBar(1)).ToHaveAttributeAsync("style", Width(50));
            await Expect(phone.Card.Verdict).ToHaveCountAsync(0);
            await Expect(phone.Card.OptionCorrect(1)).ToHaveCountAsync(0);
        }
        (await PhoneCountsAsync(first)).ShouldNotBeEmpty("the probe should find the shares on a phone once they show");
        await Expect(third.Card.NoAnswer).ToBeVisibleAsync();
        await Expect(third.Card.JoinedLate).ToHaveCountAsync(0);
        await Expect(third.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(third.Card.OptionYou(1)).ToHaveCountAsync(0);
        await Expect(first.Card.NoAnswer).ToHaveCountAsync(0);
        await Expect(screen.Correct(1)).ToHaveCountAsync(0);

        // A guest who joins after the close is told so, and sees the final result with no correct mark yet.
        await using var latePhone = await OpenPhoneAsync();
        var late = await JoinAsync(latePhone, joinUrl);
        await Expect(late.Card.JoinedLate).ToBeVisibleAsync();
        await Expect(late.Card.NoAnswer).ToHaveCountAsync(0);
        await Expect(late.Card.OptionPercent(0)).ToHaveTextAsync("50%");
        await Expect(late.Card.OptionPercent(1)).ToHaveTextAsync("50%");
        await Expect(late.Card.OptionCorrect(1)).ToHaveCountAsync(0);

        // Show answer: the big screen marks option 2, and each phone that answered says whether its guest got it; the
        // phones that didn't answer show the mark without a verdict of their own.
        await panel.ShowAnswer.ClickAsync();
        await Expect(screen.Correct(1)).ToBeVisibleAsync();
        await Expect(screen.Correct(0)).ToHaveCountAsync(0);
        await Expect(first.Card.VerdictRight).ToBeVisibleAsync();
        await Expect(first.Card.OptionCorrect(1)).ToBeVisibleAsync();
        await Expect(second.Card.VerdictMiss).ToBeVisibleAsync();
        await Expect(second.Card.OptionCorrect(1)).ToBeVisibleAsync();
        await Expect(second.Card.OptionYou(0)).ToBeVisibleAsync();
        foreach (var phone in new[] { third, late })
        {
            await Expect(phone.Card.OptionCorrect(1)).ToBeVisibleAsync();
            await Expect(phone.Card.VerdictRight).ToHaveCountAsync(0);
            await Expect(phone.Card.VerdictMiss).ToHaveCountAsync(0);
        }
    }

    // Reveal, on a poll whose results show after it closes: the big screen and every phone whose guest has answered show
    // the bars while the poll is still open, and keep moving; a phone whose guest hasn't answered stays clean until it
    // does, although its tallies now carry the counts. The correct option is marked nowhere until Show answer. A viewer
    // who can't present the deck, on the shared link, sees the same slide with empty tracks, no state and no mark, and
    // asks for no poll data, while the room sees the counts, after the close and after Show answer (D14).
    [Test]
    public async Task Poll_WhenRevealed_ShouldShowBarsWhileStillOpen()
    {
        var deck = await CreateDeckAsync("Reveal", results: "afterClose", correct: 3);
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            await AgentSeed.CallAsync(agent, "set_sharing", new() { ["deck"] = deck.Id, ["level"] = "link" });
        }
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();

        // Before the reveal nothing of the counts is on the big screen or either phone.
        (await ReachedAsync(second, Tally(answered: 1))).ShouldBeTrue("the second phone should receive the tally for the first answer");
        await second.Page.EvaluateAsync<bool>(NEXT_PAINT);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await ScreenCountsAsync(screen)).ShouldBeEmpty("the big screen before the reveal");
        (await PhoneCountsAsync(first)).ShouldBeEmpty("the answered phone before the reveal");
        (await PhoneCountsAsync(second)).ShouldBeEmpty("the unanswered phone before the reveal");
        TalliesWithCounts(second).ShouldBeEmpty("no tally with counts should reach a phone before the reveal");

        // Reveal while open: the room sees the counts now, the poll stays open, and Reveal is gone from the panel.
        await panel.Reveal.ClickAsync();
        await Expect(panel.Room).ToHaveTextAsync("Revealed: the room sees these results.");
        await Expect(panel.Reveal).ToHaveCountAsync(0);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(screen.Bar(0)).ToHaveAttributeAsync("style", Width(100));
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.Card.ResultsLater).ToHaveCountAsync(0);
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");

        // The second guest hasn't answered: its phone now receives tallies with the counts, since the room sees them,
        // and still shows no count, bar or percentage while the poll is open.
        (await ReachedAsync(second, TallyWithCounts)).ShouldBeTrue("the second phone should receive the revealed counts");
        await second.Page.EvaluateAsync<bool>(NEXT_PAINT);
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(second.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await PhoneCountsAsync(second)).ShouldBeEmpty("the unanswered phone after the reveal");

        // The viewer on the shared link, anonymous in a browser of its own, on the poll slide in the plain player. Every
        // request and hub frame it sees is kept, to show it asks for no poll data.
        await using var viewerContext = await Browser.NewContextAsync(ContextOptions());
        var viewerTab = await viewerContext.NewPageAsync();
        var viewerRequests = new ConcurrentQueue<string>();
        var viewerFrames = new ConcurrentQueue<string>();
        viewerTab.Request += (_, request) => viewerRequests.Enqueue(new Uri(request.Url).AbsolutePath);
        viewerTab.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => viewerFrames.Enqueue(frame.Text ?? "");
        var viewer = new PlayerPage(viewerTab);
        await viewer.GotoAsync(deck.Id, deck.Slug, $"#{POLL}");
        await Expect(viewer.Layer(POLL)).ToHaveAttributeAsync("data-active", "true");
        var viewerSlide = new PollSlide(viewer.Slide(POLL));
        await Expect(viewerSlide.Question).ToHaveTextAsync(QUESTION);
        await Expect(viewerSlide.Row(2)).ToBeVisibleAsync();

        // Once the second guest answers, its phone shows the bars, and they move on the big screen and the first phone.
        // The correct option is marked nowhere while the poll is open.
        await second.Card.Option(1).ClickAsync();
        await Expect(second.Card.Sent).ToBeVisibleAsync();
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.State).ToHaveTextAsync("Open");
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Card.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.Card.OptionPercent(1)).ToHaveTextAsync("50%");
            await Expect(phone.Card.OptionPercent(2)).ToHaveTextAsync("0%");
            await Expect(phone.Card.Answered).ToHaveTextAsync("2 answers so far");
        }
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }

        // The viewer who can't present still sees empty tracks: no state, no count, no bar, no percentage.
        await Expect(viewerSlide.State).ToHaveCountAsync(0);
        await Expect(viewerSlide.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(viewerSlide.Bar(position)).ToHaveCountAsync(0);
            await Expect(viewerSlide.Percent(position)).ToHaveTextAsync("");
        }
        (await ScreenCountsAsync(viewerSlide)).ShouldBeEmpty("the viewer's slide while the room sees the counts");

        // Close: the result stays on the big screen and the phones, still with no correct mark.
        await panel.Close.ClickAsync();
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(first.Card.Closed).ToContainTextAsync("2 answers");
        await Expect(second.Card.Closed).ToContainTextAsync("2 answers");
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }
        await Expect(viewerSlide.State).ToHaveCountAsync(0);
        await Expect(viewerSlide.Bar(0)).ToHaveCountAsync(0);
        (await ScreenCountsAsync(viewerSlide)).ShouldBeEmpty("the viewer's slide after the close");

        // Show answer marks option 3 on the big screen and both phones, which both missed it; the viewer gets nothing.
        await panel.ShowAnswer.ClickAsync();
        await Expect(screen.Correct(2)).ToBeVisibleAsync();
        await Expect(first.Card.OptionCorrect(2)).ToBeVisibleAsync();
        await Expect(second.Card.OptionCorrect(2)).ToBeVisibleAsync();
        await Expect(first.Card.VerdictMiss).ToBeVisibleAsync();
        await Expect(second.Card.VerdictMiss).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(viewerSlide.Correct(position)).ToHaveCountAsync(0);
            await Expect(viewerSlide.Bar(position)).ToHaveCountAsync(0);
        }
        await Expect(viewerSlide.State).ToHaveCountAsync(0);
        (await ScreenCountsAsync(viewerSlide)).ShouldBeEmpty("the viewer's slide after Show answer");
        viewerRequests.Where(path => path.Contains("/polls", StringComparison.Ordinal)).ShouldBeEmpty("the viewer should ask for no poll data");
        viewerFrames.Where(frame => frame.Contains("\"pollTally\"", StringComparison.Ordinal) || frame.Contains("\"pollControl\"", StringComparison.Ordinal))
            .ShouldBeEmpty("no poll event should reach the viewer");
    }

    // After you've answered: each phone shows the bars once its own guest has answered and follows them from then on,
    // while the big screen keeps them hidden until the poll closes; a guest who joins mid-round and never answers sees
    // no counts until the close, then the result and that it sent no answer. A guest who joins after the close is told
    // so and sees the final result, as every guest does once the poll is closed. The correct option is marked nowhere,
    // although phones show bars before the close, until Show answer.
    [Test]
    public async Task Poll_WhenResultsAfterAnswer_ShouldShowBarsOnlyOnThePhoneThatAnswered()
    {
        var deck = await CreateDeckAsync("After answer", results: "afterAnswer", correct: 2);
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room. Each guest sees them after answering.");
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.TapHint).ToBeVisibleAsync();

        // The first guest answers: its phone shows the bars; the second phone and the big screen don't.
        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.Card.OptionPercent(1)).ToHaveTextAsync("0%");
        await Expect(first.Card.OptionBar(0)).ToHaveAttributeAsync("style", Width(100));
        (await PhoneCountsAsync(first)).ShouldNotBeEmpty("the probe should find the shares on the phone that answered");
        await Expect(first.Card.ResultsLater).ToHaveCountAsync(0);
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        (await ReachedAsync(second, Tally(answered: 1))).ShouldBeTrue("the second phone should receive the tally for the first answer");
        await second.Page.EvaluateAsync<bool>(NEXT_PAINT);
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionPercent(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }
        (await ScreenCountsAsync(screen)).ShouldBeEmpty("the big screen after one answer");
        (await PhoneCountsAsync(second)).ShouldBeEmpty("the unanswered phone");

        // A third guest joins mid-round and doesn't answer: the open poll, ready to tap, with nothing of the counts.
        await using var thirdPhone = await OpenPhoneAsync();
        var third = await JoinAsync(thirdPhone, joinUrl);
        await Expect(third.Card.TapHint).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(third.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await PhoneCountsAsync(third)).ShouldBeEmpty("the guest who joined mid-round");

        // The second guest answers: its phone shows the bars too, and the first phone's move with the new answer. The
        // big screen and the third phone still hide them, and no correct option is marked.
        await second.Card.Option(2).ClickAsync();
        await Expect(second.Card.Sent).ToBeVisibleAsync();
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Card.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.Card.OptionPercent(1)).ToHaveTextAsync("0%");
            await Expect(phone.Card.OptionPercent(2)).ToHaveTextAsync("50%");
            await Expect(phone.Card.Answered).ToHaveTextAsync("2 answers so far");
        }
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        (await ReachedAsync(third, Tally(answered: 2))).ShouldBeTrue("the mid-round guest should receive the tally for the second answer");
        await third.Page.EvaluateAsync<bool>(NEXT_PAINT);
        await Expect(third.Card.TapHint).ToBeVisibleAsync();
        await Expect(third.Card.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await ScreenCountsAsync(screen)).ShouldBeEmpty("the big screen after two answers");
        (await PhoneCountsAsync(third)).ShouldBeEmpty("the mid-round guest's phone after two answers");
        TalliesWithCounts(third).ShouldBeEmpty("no tally with counts should reach a guest who hasn't answered");

        // Close: the big screen shows the result; the third guest sees it and that it sent no answer; no correct mark.
        await panel.Close.ClickAsync();
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("50%");
        (await ScreenCountsAsync(screen)).ShouldNotBeEmpty("the probe should find the shares on the big screen once they show");
        await Expect(first.Card.Closed).ToContainTextAsync("2 answers");
        await Expect(third.Card.Closed).ToContainTextAsync("2 answers");
        await Expect(third.Card.NoAnswer).ToBeVisibleAsync();
        await Expect(third.Card.OptionPercent(0)).ToHaveTextAsync("50%");
        await Expect(third.Card.OptionPercent(2)).ToHaveTextAsync("50%");
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.Card.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(third.Card.OptionCorrect(position)).ToHaveCountAsync(0);
        }

        // A guest who joins after the close is told so, and sees the final result.
        await using var latePhone = await OpenPhoneAsync();
        var late = await JoinAsync(latePhone, joinUrl);
        await Expect(late.Card.JoinedLate).ToBeVisibleAsync();
        await Expect(late.Card.NoAnswer).ToHaveCountAsync(0);
        await Expect(late.Card.Closed).ToContainTextAsync("2 answers");
        await Expect(late.Card.OptionPercent(0)).ToHaveTextAsync("50%");
        await Expect(late.Card.OptionPercent(2)).ToHaveTextAsync("50%");
        await Expect(late.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(late.Card.OptionYou(2)).ToHaveCountAsync(0);
        await Expect(late.Card.OptionCorrect(1)).ToHaveCountAsync(0);

        // Show answer marks option 2 on the big screen and every phone; both guests who answered missed it.
        await panel.ShowAnswer.ClickAsync();
        await Expect(screen.Correct(1)).ToBeVisibleAsync();
        foreach (var phone in new[] { first, second, third, late })
        {
            await Expect(phone.Card.OptionCorrect(1)).ToBeVisibleAsync();
        }
        await Expect(first.Card.VerdictMiss).ToBeVisibleAsync();
        await Expect(second.Card.VerdictMiss).ToBeVisibleAsync();
    }

    // Sam's deck: a join slide, the poll on slide 2 with three options, and a quote, with the poll's results setting and
    // correct answer set through update_poll while nothing is live.
    private static async Task<Deck> CreateDeckAsync(string title, string results, int correct)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
        var id = created.GetProperty("deck").GetString().ShouldNotBeNull();
        var slug = new Uri(created.GetProperty("url").GetString().ShouldNotBeNull()).Segments[^1];
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "join", ["slots"] = new { } });
        await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = id,
            ["layout"] = "poll",
            ["slots"] = new { question = QUESTION, options = new[] { "The morning train", "The noon train", "The night train" } },
        });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "quote", ["slots"] = new { quote = "Ship small.", attribution = "Sam Rivera" } });
        await AgentSeed.CallAsync(agent, "update_poll", new() { ["deck"] = id, ["slide"] = POLL, ["results"] = results, ["correct"] = correct });
        return new Deck(id, slug);
    }

    // Sam opens the presenter window, the audience window next to it in the same browser, and goes live from the
    // presenter window's chip. Waits for the window's join to be answered (the Now panel) and for the audience window to
    // follow before anything is pressed.
    private async Task<(PresenterPage Presenter, PlayerPage Audience, string JoinUrl)> PresentLiveAsync(Deck deck)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var viewResponse = await Page.APIRequest.GetAsync($"/api/decks/{deck.Id}/presenter");
        viewResponse.Status.ShouldBe(200, await viewResponse.TextAsync());
        var audienceUrl = JsonDocument.Parse(await viewResponse.TextAsync()).RootElement.GetProperty("audienceUrl").GetString().ShouldNotBeNull();

        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deck.Id);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        var audience = new PlayerPage(await Page.Context.NewPageAsync());
        await audience.Root.Page.GotoAsync(audienceUrl);
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);

        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.Code).ToHaveTextAsync(CodeLabel());
        var live = await Page.APIRequest.GetAsync($"/api/decks/{deck.Id}/live");
        live.Status.ShouldBe(200, await live.TextAsync());
        var joinUrl = JsonDocument.Parse(await live.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();
        return (presenter, audience, joinUrl);
    }

    // A guest scans the link and follows. Every hub frame the phone receives is kept from before it connects.
    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var frames = new ConcurrentQueue<string>();
        page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => frames.Enqueue(frame.Text ?? "");
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(page, new GuestPollCard(page), frames);
    }

    // The presenter moves to the poll slide; arrival opens it. Waits for both windows to show the move.
    private async Task MoveToPollAsync(PresenterPage presenter, PlayerPage audience)
    {
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"{POLL} / {SLIDES}");
        await Expect(audience.Layer(POLL)).ToHaveAttributeAsync("data-active", "true");
    }

    private Task<IBrowserContext> OpenPhoneAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        BaseURL = AcceptanceApp.BaseUrl,
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    private static Task<string[]> PhoneCountsAsync(Phone phone) => phone.Card.Root.EvaluateAsync<string[]>(COUNT_PROBE, PHONE_ALLOWS);

    private static Task<string[]> ScreenCountsAsync(PollSlide slide) => slide.Root.EvaluateAsync<string[]>(COUNT_PROBE, SCREEN_ALLOWS);

    // The hub messages a phone has received (a frame may hold several, each ended by the record separator).
    private static IEnumerable<string> MessagesOf(Phone phone) =>
        phone.Frames.SelectMany(frame => frame.Split('\u001e', StringSplitOptions.RemoveEmptyEntries));

    // True once a message the predicate accepts has reached the phone, or false after Eventually's 5 seconds.
    private static Task<bool> ReachedAsync(Phone phone, Func<string, bool> message) =>
        Eventually.ReadAsync(() => Task.FromResult(MessagesOf(phone).Any(message)), seen => seen);

    // A pollTally message carrying this answer count.
    private static Func<string, bool> Tally(int answered) =>
        message => message.Contains("\"pollTally\"", StringComparison.Ordinal) && Regex.IsMatch(message, $@"""answered""\s*:\s*{answered}\b");

    private static bool TallyWithCounts(string message) =>
        message.Contains("\"pollTally\"", StringComparison.Ordinal) && CountsArray().IsMatch(message);

    private static IEnumerable<string> TalliesWithCounts(Phone phone) => MessagesOf(phone).Where(TallyWithCounts).ToList();

    // The bar's inline width, as React writes it.
    private static Regex Width(int percent) => new($@"width:\s*{percent}%");

    [GeneratedRegex(@"^\d{3} \d{3}$")]
    private static partial Regex CodeLabel();

    [GeneratedRegex(@"""counts""\s*:\s*\[")]
    private static partial Regex CountsArray();

    private sealed record Deck(string Id, string Slug);

    private sealed record Phone(IPage Page, GuestPollCard Card, ConcurrentQueue<string> Frames);
}
