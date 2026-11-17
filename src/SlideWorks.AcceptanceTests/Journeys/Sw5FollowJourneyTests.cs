using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-5's main journeys on the real app, the real hub and the real presentation registry. Sam's deck goes in over MCP (a join
// slide, a bullets slide that reveals one point per step, a quote). Sam's window is the test's Page; each guest is another
// browser context at 390×844 that opens the join link the server gave, on the join host, as a scan would.
public partial class Sw5FollowJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;

    // Run on the slide element: records every "slide/step" it renders into window.__seenPositions, the one it shows now first.
    private const string RECORD_POSITIONS =
        """
        (element) => {
          const seen = [];
          const note = () => {
            const position = `${element.getAttribute('data-slide')}/${element.getAttribute('data-step')}`;
            if (seen[seen.length - 1] !== position) seen.push(position);
          };
          note();
          window.__seenPositions = seen;
          new MutationObserver(note).observe(element, { attributes: true, attributeFilter: ['data-slide', 'data-step'] });
        }
        """;

    // The path a guest takes: scan, "You're in", follow a slide and its step reveals, a second guest, the count on the owner's
    // chip and the join slide, End and the thank-you screen, and a later Go live that brings the phone back without a reload.
    [Test]
    public async Task Guest_WhenOwnerGoesLiveMovesAndEnds_ShouldFollowThenThankThenFollowAgainOnTheNextGoLive()
    {
        var deckId = await CreateDeckAsync("Journey");
        var player = await OwnerGoesLiveAsync(deckId);
        await Expect(player.JoinCode(1)).ToBeVisibleAsync();
        await Expect(player.LiveChip.Count).ToHaveTextAsync("0 joined");
        var joinUrl = await JoinUrlAsync(deckId);
        joinUrl.ShouldMatch(@"^http://localhost:\d+/\d{6}$");
        var code = joinUrl[^6..];
        await Expect(player.LiveChip.Code).ToHaveTextAsync($"{code[..3]} {code[3..]}");

        await using var phone = await OpenPhoneAsync();
        var first = new GuestPage(await phone.NewPageAsync());
        await first.GotoUrlAsync(joinUrl);
        await Expect(first.Joined).ToBeVisibleAsync();
        await first.IsFollowingAsync();
        await Expect(first.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Expect(first.Progress).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(first.Chip).ToHaveTextAsync("Live");
        await Expect(player.LiveChip.Count).ToHaveTextAsync("1 joined");
        await Expect(player.JoinCount(1)).ToHaveTextAsync("1 joined");

        await using var secondPhone = await OpenPhoneAsync();
        var second = new GuestPage(await secondPhone.NewPageAsync());
        await second.GotoUrlAsync(joinUrl);
        await second.IsFollowingAsync();
        await Expect(player.LiveChip.Count).ToHaveTextAsync("2 joined");
        await Expect(player.JoinCount(1)).ToHaveTextAsync("2 joined");

        // The player renders the presentation's answer and moves from it, so each key waits for the player to show the move
        // before the next, and the phones are checked after.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(first.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(first.Slide).ToHaveAttributeAsync("data-step", "0");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(1);
        await Expect(first.Slide).ToHaveAttributeAsync("data-step", "1");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(2);
        await Expect(first.Slide).ToHaveAttributeAsync("data-step", "2");
        await Expect(second.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(second.Slide).ToHaveAttributeAsync("data-step", "2");
        await Expect(second.Progress).ToHaveTextAsync($"2 / {SLIDES}");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(3);
        await Expect(first.Slide).ToHaveAttributeAsync("data-step", "3");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync($"3 / {SLIDES}");
        await Expect(first.Slide).ToHaveAttributeAsync("data-slide", "3");
        await Expect(first.Slide).ToHaveAttributeAsync("data-step", "0");
        await Expect(first.Progress).ToHaveTextAsync($"3 / {SLIDES}");
        await Expect(second.Slide).ToHaveAttributeAsync("data-slide", "3");
        await Expect(second.Progress).ToHaveTextAsync($"3 / {SLIDES}");

        await player.LiveChip.End.ClickAsync();
        await Expect(first.Ended).ToBeVisibleAsync();
        await Expect(first.Ended).ToContainTextAsync("Thanks for joining");
        await Expect(second.Ended).ToBeVisibleAsync();
        await Expect(player.LiveChip.GoLive).ToBeVisibleAsync();

        await first.Root.Page.EvaluateAsync("() => { window.sameDocument = true; }");
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        await Expect(first.Following).ToBeVisibleAsync();
        await Expect(first.Slide).ToHaveAttributeAsync("data-slide", "3");
        await Expect(first.Ended).ToHaveCountAsync(0);
        (await first.Root.Page.EvaluateAsync<bool>("() => window.sameDocument === true")).ShouldBeTrue("the phone should follow without a reload");
    }

    // A phone that opens the link before Go live waits in the lobby, and follows by itself once the owner goes live.
    [Test]
    public async Task Guest_WhenTheLinkIsOpenedBeforeGoLive_ShouldShowTheLobbyThenFollowWithoutReloading()
    {
        var deckId = await CreateDeckAsync("Lobby journey");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        var joinUrl = await JoinUrlAsync(deckId);

        await using var phone = await OpenPhoneAsync();
        var guest = new GuestPage(await phone.NewPageAsync());
        await guest.GotoUrlAsync(joinUrl);
        await Expect(guest.Lobby).ToBeVisibleAsync();
        await guest.Root.Page.EvaluateAsync("() => { window.sameDocument = true; }");

        await player.LiveChip.GoLive.ClickAsync();

        await Expect(guest.Following).ToBeVisibleAsync();
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Expect(player.LiveChip.Count).ToHaveTextAsync("1 joined");
        (await guest.Root.Page.EvaluateAsync<bool>("() => window.sameDocument === true")).ShouldBeTrue("the phone should follow without a reload");
    }

    // The presenter window blacks the screen out and brings it back; the phone shows the same. The presenter window, not the
    // plain player, can black out, so the owner goes live over the API for now: a chip click replaces it once the presenter window has the live chip.
    [Test]
    public async Task Guest_WhenThePresenterBlacksOutAndBack_ShouldShowABlackScreenThenTheSlide()
    {
        var deckId = await CreateDeckAsync("Blackout journey");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        var joinUrl = await GoLiveAsync(deckId);

        await using var phone = await OpenPhoneAsync();
        var guest = new GuestPage(await phone.NewPageAsync());
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "1");

        await presenter.BlackoutTool.ClickAsync();
        await Expect(guest.Slide).ToHaveAttributeAsync("data-black", "true");
        await presenter.BlackoutTool.ClickAsync();
        await Expect(guest.Slide).ToHaveAttributeAsync("data-black", "false");
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "1");

        await presenter.PressAsync("ArrowRight");
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "2");
    }

    // The phone's connection drops while the presenter moves, shows the reconnecting banner, and lands on the current slide.
    [Test]
    public async Task Guest_WhenItsHubSocketDropsWhilePresenterMoves_ShouldShowReconnectingThenLandOnTheCurrentSlide()
    {
        var deckId = await CreateDeckAsync("Reconnect journey");
        var player = await OwnerGoesLiveAsync(deckId);
        var joinUrl = await JoinUrlAsync(deckId);
        await using var phone = await OpenPhoneAsync();
        var page = await phone.NewPageAsync();
        var hub = await DroppableHub.RouteAsync(page);
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "1");

        (await hub.DropAsync()).ShouldBe(1, "the phone should have one hub connection to drop");
        await Expect(guest.Reconnecting).ToBeVisibleAsync();
        await Expect(guest.Chip).ToHaveTextAsync("Offline");
        // The player renders the presentation's answer and moves from it, so each key waits for the one before.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(1);
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "1");
        await guest.Slide.EvaluateAsync(RECORD_POSITIONS);
        hub.Release();

        await Expect(guest.Reconnecting).ToBeHiddenAsync(new() { Timeout = 20_000 });
        await Expect(guest.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Expect(guest.Slide).ToHaveAttributeAsync("data-step", "1");
        await Expect(guest.Chip).ToHaveTextAsync("Live");
        await Expect(guest.Offline).ToHaveCountAsync(0);
        var seen = await guest.Root.Page.EvaluateAsync<string[]>("() => window.__seenPositions");
        seen.ShouldNotContain("2/0", "the phone should land on the current position, not replay the moves it missed");
        seen.ShouldContain("2/1");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(guest.Slide).ToHaveAttributeAsync("data-step", "2");
    }

    // A live deck with no slides has no position to show. What the phone shows is a product call, so this holds only that it
    // stays on the guest page in a phase screen (no error, no offline screen, no slide) and doesn't crash.
    [Test]
    public async Task Guest_WhenTheLiveDeckHasNoSlides_ShouldShowAPhaseScreenWithoutAnErrorOrASlide()
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Empty live {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
        }
        await SignIn.AsAsync(Page, TestUser.Sam);
        var joinUrl = await GoLiveAsync(deckId);

        await using var phone = await OpenPhoneAsync();
        var guest = new GuestPage(await phone.NewPageAsync());
        await guest.GotoUrlAsync(joinUrl);

        await Expect(guest.Root).ToBeVisibleAsync();
        await Expect(guest.Loading).ToHaveCountAsync(0);
        await Expect(guest.Error).ToHaveCountAsync(0);
        await Expect(guest.Offline).ToHaveCountAsync(0);
        await Expect(guest.Slide).ToHaveCountAsync(0);
    }

    // Sam's deck: a join slide, a bullets slide with three points revealed one per step, and a quote.
    private static async Task<string> CreateDeckAsync(string title)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
        var id = created.GetProperty("deck").GetString().ShouldNotBeNull();
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "join", ["slots"] = new { } });
        await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = id,
            ["layout"] = "bullets",
            ["slots"] = new { headline = "Why small batches", items = new[] { "Merge every day", "Release on demand", "Learn sooner" }, reveal = "one-by-one" },
        });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
        return id;
    }

    // Signs Sam in, opens the plain player on the deck and clicks Go live there.
    private async Task<PlayerPage> OwnerGoesLiveAsync(string deckId)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.Code).ToHaveTextAsync(CodeLabel());
        return player;
    }

    private async Task<string> JoinUrlAsync(string deckId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/decks/{deckId}/live");
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();
    }

    private async Task<string> GoLiveAsync(string deckId)
    {
        var response = await Page.APIRequest.PostAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("joinUrl").GetString().ShouldNotBeNull();
    }

    private Task<IBrowserContext> OpenPhoneAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    [GeneratedRegex(@"^\d{3} \d{3}$")]
    private static partial Regex CodeLabel();
}
