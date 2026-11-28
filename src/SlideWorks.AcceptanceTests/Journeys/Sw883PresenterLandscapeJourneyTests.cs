using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-883's presenter-side and phone-orientation journeys on the real app, hub and registry. Decks and translations go in
// over MCP only (create_deck, add_slide, set_translations, translate_slides). Each test has its own owner, so nothing else
// in the shared app is live as them. The owner's presenter window is the test's Page; each guest or remote is another context.
public class Sw883PresenterLandscapeJourneyTests : AcceptanceTest
{
    private const string FIRST = "Start small.";
    private const string SECOND = "Learn sooner.";
    private const string FIRST_THAI = "เริ่มจากเล็ก ๆ";
    private const string SECOND_THAI = "เรียนรู้ได้เร็วขึ้น";
    private const string ANDROID =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36";

    // Guests read Thai; the presenter window and the phone remote stay in English, through a move.
    [Test]
    public async Task Presenter_WhenGuestsReadThai_ShouldStayInPrimary()
    {
        var owner = TestUser.NewApproved();
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(owner))
        {
            deckId = await CreateDeckAsync(agent, FIRST, SECOND);
            await AgentSeed.CallAsync(agent, "set_translations", new() { ["deck"] = deckId, ["enabled"] = true, ["primary"] = "en", ["languages"] = new[] { "th" } });
            await AgentSeed.CallAsync(agent, "translate_slides", new()
            {
                ["deck"] = deckId,
                ["language"] = "th",
                ["slides"] = new[]
                {
                    new { slide = 1, revision = await RevisionAsync(agent, deckId, 1), lines = new Dictionary<string, string> { ["/quote"] = FIRST_THAI, ["/attribution"] = "แซม" } },
                    new { slide = 2, revision = await RevisionAsync(agent, deckId, 2), lines = new Dictionary<string, string> { ["/quote"] = SECOND_THAI, ["/attribution"] = "แซม" } },
                },
            });
        }

        await SignIn.AsAsync(Page, owner);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        var joinUrl = await GoLiveAsync(deckId);

        await using var remoteContext = await OpenPhoneContextAsync();
        var remotePage = await remoteContext.NewPageAsync();
        await SignIn.AsAsync(remotePage, owner);
        var speaker = new SpeakerPage(remotePage);
        await speaker.GotoAsync(deckId);
        await speaker.IsLiveAsync();

        await using var guestContext = await LiveDeck.OpenPhoneAsync(Browser);
        var guest = await Talks.FollowAsync(await guestContext.NewPageAsync(), joinUrl);
        await Expect(guest.Slide).ToContainTextAsync(FIRST);
        await guest.LanguageChip.ClickAsync();
        await guest.LanguageOption("ไทย").ClickAsync();
        await Expect(guest.Slide).ToContainTextAsync(FIRST_THAI);
        await Expect(guest.Slide).Not.ToContainTextAsync(FIRST);

        await Expect(presenter.Now).ToContainTextAsync(FIRST);
        await Expect(presenter.Now).Not.ToContainTextAsync(FIRST_THAI);
        await Expect(speaker.Now).ToContainTextAsync(FIRST);
        await Expect(speaker.Now).Not.ToContainTextAsync(FIRST_THAI);

        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync("2 / 2");
        await Expect(guest.Slide).ToContainTextAsync(SECOND_THAI);
        await Expect(presenter.Now).ToContainTextAsync(SECOND);
        await Expect(presenter.Now).Not.ToContainTextAsync(SECOND_THAI);
        await Expect(speaker.Count).ToHaveTextAsync("2 / 2");
        await Expect(speaker.Now).ToContainTextAsync(SECOND);
        await Expect(speaker.Now).Not.ToContainTextAsync(SECOND_THAI);
    }

    // Turned sideways, a tap (on the middle of the screen, where the full-screen tap surface is; it has no test id) shows the overlay. An Android-like phone also gets Full screen; a browser with no
    // requestFullscreen does not.
    [Test]
    public async Task Guest_WhenTurnedSideways_ShouldShowOverlayOnTap()
    {
        var owner = TestUser.NewApproved();
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(owner))
        {
            deckId = await CreateDeckAsync(agent, FIRST, SECOND);
        }

        await SignIn.AsAsync(Page, owner);
        var joinUrl = await GoLiveAsync(deckId);

        await using var android = await OpenSidewaysAsync(withFullscreen: true);
        var withButton = await Talks.FollowAsync(await android.NewPageAsync(), joinUrl);
        await withButton.Root.Page.Touchscreen.TapAsync(422, 195);
        await Expect(withButton.LandscapeOverlay).ToBeVisibleAsync();
        await Expect(withButton.LandscapeFullscreen).ToBeVisibleAsync();
        await Expect(withButton.LandscapeFullscreen).ToHaveTextAsync("Full screen");

        await using var noFullscreen = await OpenSidewaysAsync(withFullscreen: false);
        var withoutButton = await Talks.FollowAsync(await noFullscreen.NewPageAsync(), joinUrl);
        await withoutButton.Root.Page.Touchscreen.TapAsync(422, 195);
        await Expect(withoutButton.LandscapeOverlay).ToBeVisibleAsync();
        await Expect(withoutButton.LandscapeFullscreen).ToHaveCountAsync(0);
    }

    // A deck that never turned translations on shows no language chip, banner or fallback note.
    [Test]
    public async Task Deck_WhenTranslationsOff_ShouldShowNoLanguageChip()
    {
        var owner = TestUser.NewApproved();
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(owner))
        {
            deckId = await CreateDeckAsync(agent, FIRST, SECOND);
        }

        await SignIn.AsAsync(Page, owner);
        var joinUrl = await GoLiveAsync(deckId);

        await using var phone = await LiveDeck.OpenPhoneAsync(Browser);
        var guest = await Talks.FollowAsync(await phone.NewPageAsync(), joinUrl);
        await Expect(guest.Slide).ToContainTextAsync(FIRST);
        await Expect(guest.Chip).ToHaveTextAsync("Live");
        await Expect(guest.LanguageChip).ToHaveCountAsync(0);
        await Expect(guest.LanguageBanner).ToHaveCountAsync(0);
        await Expect(guest.FallbackNote).ToHaveCountAsync(0);
        await Expect(guest.LanguageError).ToHaveCountAsync(0);
    }

    private static async Task<string> CreateDeckAsync(ModelContextProtocol.Client.McpClient agent, params string[] quotes)
    {
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Sideways {Guid.NewGuid():N}" });
        var id = created.GetProperty("deck").GetString().ShouldNotBeNull();
        foreach (var quote in quotes)
        {
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "quote", ["slots"] = new { quote, attribution = "Sam Rivera" } });
        }

        return id;
    }

    private static async Task<long> RevisionAsync(ModelContextProtocol.Client.McpClient agent, string deckId, int slide)
    {
        var deck = await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deckId });
        return deck.GetProperty("slides").EnumerateArray().Single(s => s.GetProperty("number").GetInt32() == slide).GetProperty("revision").GetInt64();
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

    // The owner's phone: a touch context at 390x844, for the phone remote.
    private Task<IBrowserContext> OpenPhoneContextAsync()
    {
        var options = ContextOptions();
        options.ViewportSize = new ViewportSize { Width = 390, Height = 844 };
        options.IsMobile = true;
        options.HasTouch = true;
        return Browser.NewContextAsync(options);
    }

    // An Android-like guest phone turned sideways (844x390, mobile, touch). Without fullscreen the page has no
    // requestFullscreen, as a browser that cannot do it.
    private async Task<IBrowserContext> OpenSidewaysAsync(bool withFullscreen)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = AcceptanceApp.BaseUrl,
            ViewportSize = new ViewportSize { Width = 844, Height = 390 },
            IsMobile = true,
            HasTouch = true,
            UserAgent = ANDROID,
        });
        if (!withFullscreen)
        {
            await context.AddInitScriptAsync("Object.defineProperty(Element.prototype, 'requestFullscreen', { value: undefined, configurable: true, writable: true });");
        }

        return context;
    }
}
