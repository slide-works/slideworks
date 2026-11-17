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
// A journey sees only the rendered page, so "hidden means hidden" is checked on the DOM: while counts are hidden there is
// no bar element, no percentage and no width in any attribute or text of the slide's rows or a phone's options (the
// raw frames and bodies are covered at the server tier). The phone shows "N answers so far" only once its own guest
// has answered (PollFoot), the same before the bars are revealed as after (D21 holds on the wire; the phone keeps the
// card clean while choosing, as the mockup's phone states have it).
public partial class Sw6PollResultsJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const string QUESTION = "Which release train do we board?";

    // Returns everything under the element that could carry a count or a share: any attribute whose value holds a
    // percentage or a width, and any text that holds a percentage. Empty while counts are hidden.
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

    // After the poll closes: while open, the big screen hatches empty tracks and counts the answers, and a phone that
    // answered confirms it and says when results show, with no bar or percentage anywhere; the presenter's panel alone
    // has the counts. Close shows the final result on the big screen and on every phone, and Show answer then marks the
    // correct option there and tells each phone whether its guest got it.
    [Test]
    public async Task Poll_WhenResultsAfterClose_ShouldHideBarsOnScreenAndPhoneUntilClosed()
    {
        var deck = await CreateDeckAsync("After close", results: "afterClose", correct: 2);
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var (_, first) = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var (_, second) = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(panel.State).ToHaveTextAsync("Open");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room until the poll closes.");
        await Expect(first.Root).ToBeVisibleAsync();
        await Expect(first.TapHint).ToBeVisibleAsync();
        await Expect(second.TapHint).ToBeVisibleAsync();

        // The first guest picks the correct option. Its phone confirms and says when results show; the second phone,
        // still choosing, shows no count.
        await first.Option(1).ClickAsync();
        await Expect(first.Sent).ToBeVisibleAsync();
        await Expect(first.OptionYou(1)).ToBeVisibleAsync();
        await Expect(first.ResultsLater).ToBeVisibleAsync();
        await Expect(first.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(second.Answered).ToHaveCountAsync(0);
        await Expect(second.TapHint).ToBeVisibleAsync();

        // The second guest picks another option.
        await second.Option(0).ClickAsync();
        await Expect(second.Sent).ToBeVisibleAsync();
        await Expect(second.ResultsLater).ToBeVisibleAsync();
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(first.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(second.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(panel.Count(0)).ToHaveTextAsync("1");
        await Expect(panel.Count(1)).ToHaveTextAsync("1");
        await Expect(panel.Count(2)).ToHaveTextAsync("0");

        // Hidden means hidden: nothing of the counts is in the big screen's rows or either phone's options, and the
        // correct option is not marked anywhere.
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
            await Expect(screen.Correct(position)).ToHaveCountAsync(0);
            await Expect(first.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(first.OptionPercent(position)).ToHaveCountAsync(0);
            await Expect(first.OptionCorrect(position)).ToHaveCountAsync(0);
            await Expect(second.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the big screen's rows while hidden");
        (await first.Options.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the first phone's options while hidden");
        (await second.Options.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the second phone's options while hidden");

        // Close: the final result on the big screen and on both phones.
        await panel.Close.ClickAsync();
        await Expect(panel.State).ToHaveTextAsync("Closed");
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(screen.Bar(0)).ToHaveAttributeAsync("style", Width(50));
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldNotBeEmpty("the probe should find the shares once they show");
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.Closed).ToContainTextAsync("2 answers");
            await Expect(phone.ResultsLater).ToHaveCountAsync(0);
            await Expect(phone.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.OptionPercent(1)).ToHaveTextAsync("50%");
            await Expect(phone.OptionPercent(2)).ToHaveTextAsync("0%");
            await Expect(phone.OptionBar(1)).ToHaveAttributeAsync("style", Width(50));
            await Expect(phone.Verdict).ToHaveCountAsync(0);
            await Expect(phone.OptionCorrect(1)).ToHaveCountAsync(0);
        }
        await Expect(screen.Correct(1)).ToHaveCountAsync(0);

        // Show answer: the big screen marks option 2, and each phone says whether its guest got it.
        await panel.ShowAnswer.ClickAsync();
        await Expect(screen.Correct(1)).ToBeVisibleAsync();
        await Expect(screen.Correct(0)).ToHaveCountAsync(0);
        await Expect(first.VerdictRight).ToBeVisibleAsync();
        await Expect(first.OptionCorrect(1)).ToBeVisibleAsync();
        await Expect(second.VerdictMiss).ToBeVisibleAsync();
        await Expect(second.OptionCorrect(1)).ToBeVisibleAsync();
        await Expect(second.OptionYou(0)).ToBeVisibleAsync();
    }

    // Reveal, on a poll whose results show after it closes: the big screen and every phone whose guest has answered show
    // the bars while the poll is still open, and keep moving; a phone whose guest hasn't answered stays clean until it
    // does. A viewer who can't present the deck, on the shared link, sees the same slide with empty tracks and no state
    // (D14), even while the room sees the counts.
    [Test]
    public async Task Poll_WhenRevealed_ShouldShowBarsWhileStillOpen()
    {
        var deck = await CreateDeckAsync("Reveal", results: "afterClose", correct: null);
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            await AgentSeed.CallAsync(agent, "set_sharing", new() { ["deck"] = deck.Id, ["level"] = "link" });
        }
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var (_, first) = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var (_, second) = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(first.TapHint).ToBeVisibleAsync();
        await first.Option(0).ClickAsync();
        await Expect(first.Sent).ToBeVisibleAsync();
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        await Expect(screen.Bar(0)).ToHaveCountAsync(0);
        await Expect(first.OptionPercent(0)).ToHaveCountAsync(0);

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
        await Expect(first.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.ResultsLater).ToHaveCountAsync(0);
        await Expect(first.Answered).ToHaveTextAsync("1 answer so far");

        // The second guest hasn't answered: while the poll is open its phone shows no count, bar or percentage, even
        // though the room sees them.
        await Expect(second.TapHint).ToBeVisibleAsync();
        await Expect(second.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(second.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await second.Options.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the unanswered phone's options");

        // The viewer on the shared link, anonymous in a browser of its own, on the poll slide in the plain player.
        await using var viewerContext = await Browser.NewContextAsync(ContextOptions());
        var viewer = new PlayerPage(await viewerContext.NewPageAsync());
        await viewer.GotoAsync(deck.Id, deck.Slug, $"#{POLL}");
        await Expect(viewer.Layer(POLL)).ToHaveAttributeAsync("data-active", "true");
        var viewerSlide = new PollSlide(viewer.Slide(POLL));
        await Expect(viewerSlide.Question).ToHaveTextAsync(QUESTION);
        await Expect(viewerSlide.Row(2)).ToBeVisibleAsync();

        // Once the second guest answers, its phone shows the bars, and they move on the big screen and the first phone.
        await second.Option(1).ClickAsync();
        await Expect(second.Sent).ToBeVisibleAsync();
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(screen.State).ToHaveTextAsync("Open");
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.OptionPercent(1)).ToHaveTextAsync("50%");
            await Expect(phone.OptionPercent(2)).ToHaveTextAsync("0%");
            await Expect(phone.Answered).ToHaveTextAsync("2 answers so far");
        }

        // The viewer who can't present still sees empty tracks: no state, no count, no bar, no percentage.
        await Expect(viewerSlide.State).ToHaveCountAsync(0);
        await Expect(viewerSlide.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(viewerSlide.Bar(position)).ToHaveCountAsync(0);
            await Expect(viewerSlide.Percent(position)).ToHaveTextAsync("");
        }
        (await viewerSlide.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the viewer's rows");
    }

    // After you've answered: each phone shows the bars once its own guest has answered and follows them from then on,
    // while the big screen keeps them hidden until the poll closes. A guest who joins after the close is told so and sees
    // the final result, as every guest does once the poll is closed.
    [Test]
    public async Task Poll_WhenResultsAfterAnswer_ShouldShowBarsOnlyOnThePhoneThatAnswered()
    {
        var deck = await CreateDeckAsync("After answer", results: "afterAnswer", correct: null);
        var (presenter, audience, joinUrl) = await PresentLiveAsync(deck);
        await using var firstPhone = await OpenPhoneAsync();
        var (_, first) = await JoinAsync(firstPhone, joinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var (_, second) = await JoinAsync(secondPhone, joinUrl);
        var panel = new PollPanel(Page);
        var screen = new PollSlide(audience.Slide(POLL));

        await MoveToPollAsync(presenter, audience);
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(panel.Room).ToHaveTextAsync("Hidden from the room. Each guest sees them after answering.");
        await Expect(first.TapHint).ToBeVisibleAsync();
        await Expect(second.TapHint).ToBeVisibleAsync();

        // The first guest answers: its phone shows the bars; the second phone and the big screen don't.
        await first.Option(0).ClickAsync();
        await Expect(first.Sent).ToBeVisibleAsync();
        await Expect(first.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.OptionPercent(1)).ToHaveTextAsync("0%");
        await Expect(first.OptionBar(0)).ToHaveAttributeAsync("style", Width(100));
        (await first.Options.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldNotBeEmpty("the probe should find the shares on the phone that answered");
        await Expect(first.ResultsLater).ToHaveCountAsync(0);
        await Expect(first.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        await Expect(second.TapHint).ToBeVisibleAsync();
        await Expect(second.Answered).ToHaveCountAsync(0);
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
            await Expect(screen.Percent(position)).ToHaveTextAsync("");
            await Expect(second.OptionBar(position)).ToHaveCountAsync(0);
            await Expect(second.OptionPercent(position)).ToHaveCountAsync(0);
        }
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the big screen's rows");
        (await second.Options.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the unanswered phone's options");

        // The second guest answers: its phone shows the bars too, and the first phone's move with the new answer. The
        // big screen still hides them.
        await second.Option(2).ClickAsync();
        await Expect(second.Sent).ToBeVisibleAsync();
        foreach (var phone in new[] { first, second })
        {
            await Expect(phone.OptionPercent(0)).ToHaveTextAsync("50%");
            await Expect(phone.OptionPercent(1)).ToHaveTextAsync("0%");
            await Expect(phone.OptionPercent(2)).ToHaveTextAsync("50%");
            await Expect(phone.Answered).ToHaveTextAsync("2 answers so far");
        }
        await Expect(screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(screen.State).ToHaveTextAsync("Open");
        await Expect(screen.HiddenNote).ToBeVisibleAsync();
        foreach (var position in Enumerable.Range(0, 3))
        {
            await Expect(screen.Bar(position)).ToHaveCountAsync(0);
        }
        (await screen.Rows.EvaluateAsync<string[]>(COUNT_PROBE)).ShouldBeEmpty("the big screen's rows after two answers");

        // Close: the big screen shows the result.
        await panel.Close.ClickAsync();
        await Expect(screen.State).ToHaveTextAsync("Closed");
        await Expect(screen.HiddenNote).ToHaveCountAsync(0);
        await Expect(screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(screen.Percent(2)).ToHaveTextAsync("50%");
        await Expect(first.Closed).ToContainTextAsync("2 answers");

        // A guest who joins after the close is told so, and sees the final result.
        await using var latePhone = await OpenPhoneAsync();
        var (_, late) = await JoinAsync(latePhone, joinUrl);
        await Expect(late.JoinedLate).ToBeVisibleAsync();
        await Expect(late.NoAnswer).ToHaveCountAsync(0);
        await Expect(late.Closed).ToContainTextAsync("2 answers");
        await Expect(late.OptionPercent(0)).ToHaveTextAsync("50%");
        await Expect(late.OptionPercent(2)).ToHaveTextAsync("50%");
        await Expect(late.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(late.OptionYou(2)).ToHaveCountAsync(0);
    }

    // Sam's deck: a join slide, the poll on slide 2 with three options, and a quote, with the poll's results setting and
    // correct answer set through update_poll while nothing is live.
    private static async Task<Deck> CreateDeckAsync(string title, string results, int? correct)
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
        var settings = new Dictionary<string, object?> { ["deck"] = id, ["slide"] = POLL, ["results"] = results };
        if (correct is not null)
        {
            settings["correct"] = correct;
        }
        await AgentSeed.CallAsync(agent, "update_poll", settings);
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

    // A guest scans the link and follows; the phone shows the join slide's waiting card until the presenter moves on.
    private static async Task<(GuestPage Guest, GuestPollCard Card)> JoinAsync(IBrowserContext phone, string joinUrl)
    {
        var page = await phone.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return (guest, new GuestPollCard(page));
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

    // The bar's inline width, as React writes it.
    private static Regex Width(int percent) => new($@"width:\s*{percent}%");

    [GeneratedRegex(@"^\d{3} \d{3}$")]
    private static partial Regex CodeLabel();

    private sealed record Deck(string Id, string Slug);
}
