using System.Globalization;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-4's laptop failure paths on the real app and the real hub: the presenter window loses its hub connection and gets
// it back, and the presenter runs the timer and loses and reopens the audience window. Sam's deck goes in over MCP
// (create_deck, add_slide). The presenter window is the test's Page and the audience window its popup in the same
// browser context, opened by the deck page's Presenter view or by the presenter window's own Reopen.
public partial class PresentingRecoveryJourneyTests : AcceptanceTest
{
    private const int SLIDES = 4;

    // D4, D9: a dropped presenter shows Reconnecting while the audience window carries on. On reconnect the presenter
    // takes the server's state (the move the audience window made while it was gone, not its own resume), so both
    // windows agree, and the timer the server holds kept counting through the drop.
    [Test]
    public async Task PresenterWindow_WhenItsHubSocketDropsAndComesBack_ShouldShowReconnectingThenAgreeWithTheAudienceAndKeepTheTimer()
    {
        var hub = await DroppableHub.RouteAsync(Page);
        var id = await CreateDeckAsync("Recovery");
        var (presenter, audience) = await OpenPresenterWindowAsync(id);

        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(audience.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");
        await Expect(presenter.Timer).Not.ToHaveTextAsync("0:00");
        var before = await SecondsAsync(presenter.Timer);

        (await hub.DropAsync()).ShouldBe(1, "the presenter window should have one hub connection to drop");
        await Expect(presenter.Reconnecting).ToBeVisibleAsync();
        await Expect(presenter.Reconnecting).ToContainTextAsync("Reconnecting");
        await audience.Root.Page.Keyboard.PressAsync("ArrowRight");
        await Expect(audience.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        // The window's own tick carries on while it reconnects: hold the drop until it shows 3 more seconds, long enough
        // that a server timer stopped, restarted or rolled back by the drop would show once the rejoin replaces it.
        await Expect(presenter.Timer).ToHaveTextAsync(AtLeast(before + 3), new() { Timeout = 15_000 });
        hub.Release();

        await Expect(presenter.Reconnecting).ToBeHiddenAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"3 / {SLIDES}");
        await Expect(audience.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");
        var after = await SecondsAsync(presenter.Timer);
        after.ShouldBeGreaterThanOrEqualTo(before + 3, "the server's timer should have kept counting through the drop");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);

        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"4 / {SLIDES}");
        await Expect(audience.Layer(4)).ToHaveAttributeAsync("data-active", "true");
        await Expect(audience.Layer(3)).ToHaveAttributeAsync("data-active", "false");
        await Expect(presenter.Error).ToHaveCountAsync(0);
    }

    // P2's timer from the presenter window: it waits for the first move, pause stops it and reset takes it back to 0:00.
    // Closing the audience window says so in the presenter window, and Reopen brings it back on the current slide. The
    // browser's clock is Playwright's, so the windows' performance.now() ticks can be run forward without waiting; the
    // server's clock stays real.
    [Test]
    public async Task PresenterWindow_WhenTimerIsDrivenAndAudienceWindowIsClosed_ShouldStartPauseResetAndReopenOnTheCurrentSlide()
    {
        await Page.Clock.InstallAsync();
        var id = await CreateDeckAsync("Timer");
        var (presenter, audience) = await StartPresentingAsync(id);

        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "notStarted");
        await Page.Clock.RunForAsync(3000);
        await Expect(presenter.Timer).ToHaveTextAsync("0:00");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "notStarted");

        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");
        await Page.Clock.RunForAsync(3000);
        await Expect(presenter.Timer).ToHaveTextAsync(AtLeast(3));

        await presenter.TimerPause.ClickAsync();
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "paused");
        var paused = await presenter.Timer.TextContentAsync();
        await Page.Clock.RunForAsync(5000);
        await Expect(presenter.Timer).ToHaveTextAsync(paused!);
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "paused");

        await presenter.TimerReset.ClickAsync();
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "notStarted");
        await Expect(presenter.Timer).ToHaveTextAsync("0:00");
        await Expect(presenter.Error).ToHaveCountAsync(0);

        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"3 / {SLIDES}");
        await Expect(audience.Layer(3)).ToHaveAttributeAsync("data-active", "true");

        await audience.Root.Page.CloseAsync();
        await Expect(presenter.AudienceClosed).ToBeVisibleAsync();
        await Expect(presenter.AudienceClosed).ToContainTextAsync("Audience window closed");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(0);
        await Expect(presenter.NoAudienceChip).ToBeVisibleAsync();

        var reopened = new PlayerPage(await Page.RunAndWaitForPopupAsync(() => presenter.AudienceReopen.ClickAsync()));
        await Expect(reopened.AudienceMode).ToBeVisibleAsync();
        await Expect(reopened.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        await Expect(reopened.Layer(1)).ToHaveAttributeAsync("data-active", "false");
        await Expect(presenter.AudienceClosed).ToBeHiddenAsync();
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Expect(presenter.Count).ToHaveTextAsync($"3 / {SLIDES}");
    }

    // Sam's deck with SLIDES quote slides, made the way an agent makes it.
    private static async Task<string> CreateDeckAsync(string title)
    {
        string id;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            id = created.GetProperty("deck").GetString()!;
        }
        var quotes = Enumerable.Range(1, SLIDES).Select(n => $"Quote number {n}.").ToArray();
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, quotes);
        return id;
    }

    // Signs Sam in, opens the deck page and picks Presenter view: this tab becomes the presenter window and the audience
    // window opens as a popup. Returns both once each has joined the presentation on slide 1.
    private async Task<(PresenterPage Presenter, PlayerPage Audience)> StartPresentingAsync(string id)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var deck = new DeckPage(Page);
        await deck.GotoAsync(id);
        await deck.PresentMenu.ClickAsync();
        var popup = await Page.RunAndWaitForPopupAsync(() => deck.PresentMenuPresenterView.ClickAsync());
        await Expect(Page).ToHaveURLAsync(PresenterPath());
        return await BothJoinedAsync(new PresenterPage(Page), new PlayerPage(popup));
    }

    // Signs Sam in and opens the presenter window straight at its URL, as after a reload, then opens the audience window
    // with the top bar's Reopen.
    private async Task<(PresenterPage Presenter, PlayerPage Audience)> OpenPresenterWindowAsync(string id)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(id);
        await presenter.IsLiveAsync();
        await Expect(presenter.NoAudienceChip).ToBeVisibleAsync();
        var popup = await Page.RunAndWaitForPopupAsync(() => presenter.TopBarReopenAudience.ClickAsync());
        return await BothJoinedAsync(presenter, new PlayerPage(popup));
    }

    // Waits until both windows have joined the presentation on slide 1.
    private async Task<(PresenterPage Presenter, PlayerPage Audience)> BothJoinedAsync(PresenterPage presenter, PlayerPage audience)
    {
        await presenter.IsLiveAsync();
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        return (presenter, audience);
    }

    // The timer's text ("m:ss", or "h:mm:ss" from an hour) as whole seconds.
    private static async Task<int> SecondsAsync(ILocator timer)
    {
        var text = await timer.TextContentAsync() ?? string.Empty;
        return text.Split(':').Aggregate(0, (total, part) => (total * 60) + int.Parse(part, CultureInfo.InvariantCulture));
    }

    // Matches the timer's text for {seconds} or up to two minutes more, written as format.ts writes it under an hour.
    private static Regex AtLeast(int seconds) =>
        new($"^({string.Join('|', Enumerable.Range(seconds, 121).Select(s => $"{s / 60}:{s % 60:00}"))})$");

    [GeneratedRegex(@"/decks/[0-9A-Za-z]{12}/present$")]
    private static partial Regex PresenterPath();
}
