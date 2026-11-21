using System.Collections.Concurrent;
using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Reactions and the poll switch on the real app (SW-9 D6, D7), through the real hub. Sam's deck (a join slide, a quote and
// a pick-one poll that opens on arrival) goes in over MCP. Sam presents from the presenter window (the test's Page, desktop
// size) with the audience window, the big screen, as a second tab. Each guest is its own phone-sized browser context that
// opens the join link the server gave. Floating emoji are animated, so the big screen is watched for the nodes that
// appear (a mutation observer installed before the tap) and the presenter's counter is asserted as text; nothing here
// depends on animation timing.
public class Sw9ReactionsJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 3;
    private const string REACTIONS_TYPE = "\"type\":\"reactions\"";
    private const string QUESTION = "How big was your last change?";

    // The first assertion after a hub push waits as long as the repo's other journeys do (15 s).
    private const float PUSH_TIMEOUT = 15_000;

    // Records every float node the reactions layer gets, with its text, before the animation retires it.
    private const string WATCH_FLOATS =
        """
        floatTestId => {
          window.__floatsSeen = [];
          const note = node => {
            if (node.nodeType !== 1) return;
            for (const el of [node, ...node.querySelectorAll(`[data-testid="${floatTestId}"]`)]) {
              if (el.getAttribute('data-testid') === floatTestId) window.__floatsSeen.push(el.textContent);
            }
          };
          new MutationObserver(records => records.forEach(r => r.addedNodes.forEach(note)))
            .observe(document.body, { childList: true, subtree: true });
          return true;
        }
        """;

    // Criteria: a clap tapped on one phone floats on the audience window and adds to the presenter panel's counter, and
    // another phone's page has no reaction layer and shows nothing of it (D6: other phones don't receive them).
    [Test]
    public async Task Reaction_WhenTapped_ShouldFloatOnTheBigScreenAndCountForThePresenterOnly()
    {
        var talk = await PresentAsync("Reactions");
        await using var contextA = await PollJourney.OpenPhoneContextAsync(Browser);
        var phoneA = await JoinAsync(contextA, talk.JoinUrl);
        await using var contextB = await PollJourney.OpenPhoneContextAsync(Browser);
        var phoneB = await JoinAsync(contextB, talk.JoinUrl);
        var panel = new QaPanel(Page);
        await talk.Presenter.PressAsync("q");
        await Expect(panel.Root).ToBeVisibleAsync();
        await Expect(panel.CounterEmpty).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(phoneA.Questions.ReactionRow).ToBeVisibleAsync();
        await Expect(phoneB.Questions.ReactionRow).ToBeVisibleAsync();
        await talk.AudiencePage.EvaluateAsync<bool>(WATCH_FLOATS, QaSlide.FLOAT_TEST_ID);
        var screen = new QaSlide(talk.Audience.Slide(1));
        await Expect(screen.ReactionsLayer).ToHaveCountAsync(1);
        var phoneBStatesBefore = StateFrames(phoneB.Frames);

        await phoneA.Questions.Reaction("clap").ClickAsync();

        await Expect(panel.Count("clap")).ToContainTextAsync("1", new() { Timeout = PUSH_TIMEOUT });
        await Expect(panel.Count("clap")).ToContainTextAsync("👏");
        await Expect(panel.CounterEmpty).ToHaveCountAsync(0);
        await talk.AudiencePage.WaitForFunctionAsync("() => window.__floatsSeen.length >= 1", null, new() { Timeout = PUSH_TIMEOUT });
        var floats = await talk.AudiencePage.EvaluateAsync<string[]>("() => window.__floatsSeen");
        floats.Length.ShouldBe(1);
        floats[0].ShouldContain("👏");
        // The other phone received nothing. Its page never draws reactions, so its hub frames are what is checked. They are
        // recorded from before it connected. A later event of another type must still reach it after the tap, or a dead
        // socket would pass; then no frame it ever received is a reactions burst.
        await talk.Presenter.PressAsync("ArrowRight");
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"2 / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
        (await Eventually.ReadAsync(() => Task.FromResult(StateFrames(phoneB.Frames)), count => count > phoneBStatesBefore))
            .ShouldBeGreaterThan(phoneBStatesBefore, "phone B's socket should still deliver events after the tap");
        phoneB.Frames.ShouldNotBeEmpty();
        phoneB.Frames.Where(frame => frame.Contains(REACTIONS_TYPE, StringComparison.Ordinal)).ShouldBeEmpty();
        await Expect(phoneB.Questions.ReactionError).ToHaveCountAsync(0);
    }

    private static int StateFrames(ConcurrentQueue<string> frames) =>
        frames.Count(frame => frame.Contains("\"type\":\"audienceState\"", StringComparison.Ordinal));

    // Criteria (D7): a phone on the Questions tab switches to the Slide tab, and shows the poll, when the poll opens; a phone
    // with the ask sheet open stays on Questions and shows "A poll just opened. It is on the Slide tab." with Go to poll,
    // which closes the sheet and shows the Slide tab and the poll.
    [Test]
    public async Task Poll_WhenOpensWhileOnQuestions_ShouldSwitchToTheSlideTab()
    {
        var talk = await PresentAsync("Poll switch");
        await using var contextA = await PollJourney.OpenPhoneContextAsync(Browser);
        var plain = await JoinAsync(contextA, talk.JoinUrl);
        await using var contextB = await PollJourney.OpenPhoneContextAsync(Browser);
        var asking = await JoinAsync(contextB, talk.JoinUrl);
        foreach (var phone in new[] { plain, asking })
        {
            await phone.Questions.QuestionsTab.ClickAsync();
            await Expect(phone.Questions.List).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
            await Expect(phone.Questions.ListLoading).ToHaveCountAsync(0);
            await Expect(phone.Questions.QuestionsPanel).ToBeVisibleAsync();
        }
        await asking.Questions.Ask.ClickAsync();
        await Expect(asking.Questions.AskSheet).ToBeVisibleAsync();
        await Expect(asking.Questions.PollOpened).ToHaveCountAsync(0);

        // One move at a time, each waited on, so the second key isn't lost to the first move still in flight.
        foreach (var to in new[] { 2, POLL })
        {
            await talk.Presenter.PressAsync("ArrowRight");
            await Expect(talk.Presenter.Count).ToHaveTextAsync($"{to} / {SLIDES}", new() { Timeout = PUSH_TIMEOUT });
            await Expect(talk.Audience.Layer(to)).ToHaveAttributeAsync("data-active", "true", new() { Timeout = PUSH_TIMEOUT });
        }

        // The plain phone switches by itself: the poll card shows and the Questions list is gone.
        await Expect(plain.Card.Root).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(plain.Card.Question).ToHaveTextAsync(QUESTION);
        await Expect(plain.Questions.SlidePanel).ToBeVisibleAsync();
        await Expect(plain.Questions.QuestionsPanel).ToBeHiddenAsync();
        await Expect(plain.Questions.SlideTab).ToHaveAttributeAsync("aria-selected", "true");
        // The asking phone stays: the sheet shows the banner, the Questions tab is still the one showing.
        await Expect(asking.Questions.PollOpened).ToBeVisibleAsync(new() { Timeout = PUSH_TIMEOUT });
        await Expect(asking.Questions.PollOpenedMessage).ToHaveTextAsync("A poll just opened. It is on the Slide tab.");
        await Expect(asking.Questions.AskSheet).ToBeVisibleAsync();
        await Expect(asking.Questions.QuestionsPanel).ToBeVisibleAsync();
        await Expect(asking.Questions.SlidePanel).ToBeHiddenAsync();

        await asking.Questions.PollOpenedGoToPoll.ClickAsync();

        await Expect(asking.Questions.AskSheet).ToHaveCountAsync(0);
        await Expect(asking.Questions.SlidePanel).ToBeVisibleAsync();
        await Expect(asking.Questions.SlideTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(asking.Card.Root).ToBeVisibleAsync();
        await Expect(asking.Card.Question).ToHaveTextAsync(QUESTION);
        await Expect(asking.Questions.PollOpened).ToHaveCountAsync(0);
    }

    // Sam's deck over MCP, then the presenter window live with the audience window beside it. On slide 1 when this returns.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
            await AgentSeed.CallAsync(agent, "add_slide", new()
            {
                ["deck"] = deckId,
                ["layout"] = "poll",
                ["slots"] = new { question = QUESTION, options = new[] { "A few lines", "A few files", "A whole feature" } },
            });
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
        var audience = new PlayerPage(audienceTab);
        var audienceRead = await PollJourney.ReadAsync(Page, $"/api/decks/{deckId}/presenter", "audienceUrl");
        audienceRead.Status.ShouldBe(200, audienceRead.Body);
        await audienceTab.GotoAsync(audienceRead.Value.ShouldNotBeNull());
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Page.BringToFrontAsync();
        return new Talk(presenter, audience, audienceTab, joinUrl);
    }

    // A guest scans the code and follows the presenter, on the Slide tab. The page holds its hub connection, so it is on the roster.
    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var frames = new ConcurrentQueue<string>();
        page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => frames.Enqueue(frame.Text ?? "");
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(page, guest, new GuestQuestions(page), new GuestPollCard(page), frames);
    }

    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, IPage AudiencePage, string JoinUrl);

    private sealed record Phone(IPage Page, GuestPage Guest, GuestQuestions Questions, GuestPollCard Card, ConcurrentQueue<string> Frames);
}
