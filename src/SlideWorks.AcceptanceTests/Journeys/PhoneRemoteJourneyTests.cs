using System.Globalization;
using SlideWorks.AcceptanceTests.Journeys.Support;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-4 journeys for the phone (E1b): the owner presents from a phone on the real app and the real hub. The laptop's
// presenter window runs in the test's own context; the phone is a second context of the same owner at a phone
// viewport. Decks go in over MCP (create_deck, then add_slide for each slide), and the talk length over the PUT the
// deck page uses.
public partial class PhoneRemoteJourneyTests : AcceptanceTest
{
    private const int SLIDES = 12;

    // P8, P2, P7: the phone picks the live talk on /remote, its Next moves the presenter window, a talk length set
    // while the talk runs shows as time left, and a jump from the slides sheet moves the presenter too.
    [Test]
    public async Task PhoneDrivesTheTalk_WhenOwnerPicksItOnRemoteWhileThePresenterWindowIsOpen_ShouldMoveThePresenterAndShowTimeLeft()
    {
        var title = $"Phone remote {Guid.NewGuid():N}";
        var id = await CreateDeckAsync(TestUser.Sam, title);
        var presenter = await OpenPresenterAsync(TestUser.Sam, id);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");

        await using var phone = await OpenPhoneAsync(TestUser.Sam);
        var remote = new RemotePage(phone.Page);
        await remote.GotoAsync();
        await remote.IsLoadedAsync();
        var card = remote.CardTitled(title);
        await Expect(card).ToHaveCountAsync(1);
        await RemotePage.ControlLinkOf(card).ClickAsync();

        var speaker = new SpeakerPage(phone.Page);
        await Expect(phone.Page).ToHaveURLAsync(SpeakerUrl(id));
        await speaker.IsLiveAsync();
        await Expect(speaker.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(speaker.TimeLeft).ToHaveCountAsync(0);

        // The talk length goes in before the first Next, so the timer has not started and time left is exactly the 30 minutes.
        var response = await Page.APIRequest.PutAsync($"/api/decks/{id}/talk-length", new APIRequestContextOptions
        {
            DataObject = new { minutes = 30 },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(204, await response.TextAsync());
        await Expect(speaker.Timer).ToHaveAttributeAsync("data-state", "notStarted");
        await Expect(speaker.TimeLeft).ToHaveTextAsync("30:00 left");
        await Expect(presenter.TimeLeft).ToHaveTextAsync("30:00 left of 30:00");

        await speaker.Next.ClickAsync();

        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(speaker.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(speaker.Now).ToHaveAttributeAsync("data-slide", "2");

        // Next started the timer: it runs on the phone and the presenter window, and the 30 minutes have begun to go.
        await Expect(speaker.Timer).ToHaveAttributeAsync("data-state", "running");
        await Expect(presenter.Timer).ToHaveAttributeAsync("data-state", "running");
        await Expect(speaker.TimeLeft).ToHaveTextAsync(TimeLeftOnceRunning());
        await Expect(presenter.TimeLeft).ToHaveTextAsync(PresenterTimeLeftOnceRunning());

        await speaker.JumpToAsync(11);

        await Expect(presenter.Count).ToHaveTextAsync($"11 / {SLIDES}");
        await Expect(speaker.Count).ToHaveTextAsync($"11 / {SLIDES}");
        await Expect(speaker.Now).ToHaveAttributeAsync("data-slide", "11");
    }

    private const string LIVE_CARD_PROBE =
        "root => [...root.querySelectorAll('[data-testid=\"remote-live-card\"]')].map(card => card.textContent ?? '')";

    // P4, P8: /remote with nothing live shows the empty note; once the owner's presenter window is open, the QR code's
    // link (/remote?deck={id}) goes straight to that deck's speaker mode.
    [Test]
    public async Task PhoneOpensRemote_WhenNothingIsLiveAndThenTheQrLinkNamesALiveDeck_ShouldShowEmptyThenGoStraightToSpeakerMode()
    {
        // A fresh person: nothing else in the shared app presents as them, so nothing of theirs can be live.
        var owner = TestUser.NewApproved();
        await using var phone = await OpenPhoneAsync(owner);
        var remote = new RemotePage(phone.Page);
        await remote.GotoAsync();
        await remote.IsLoadedAsync();
        await Expect(remote.Empty).ToBeVisibleAsync();

        // The same probe runs before a deck of theirs is live (it finds no card) and once it is (it finds the card), so a
        // broken locator cannot pass the empty half.
        string id = "";
        var probe = await Probes.PairedAsync(remote.Root, LIVE_CARD_PROBE, async () =>
        {
            id = await CreateDeckAsync(owner, $"Straight to speaker {Guid.NewGuid():N}");
            var presenter = await OpenPresenterAsync(owner, id);
            await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
            await remote.GotoAsync();
            await remote.IsLoadedAsync();
            await Expect(remote.LiveCards).ToHaveCountAsync(1);
        });
        probe.Absent.ShouldBeEmpty("live cards before anything is presenting");
        probe.Present.ShouldNotBeEmpty("the probe should find the card once the deck is live");

        await remote.GotoAsync(id);

        var speaker = new SpeakerPage(phone.Page);
        await Expect(phone.Page).ToHaveURLAsync(SpeakerUrl(id));
        await speaker.IsLoadedAsync();
        await speaker.IsLiveAsync();
        await Expect(speaker.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(speaker.Now).ToHaveAttributeAsync("data-slide", "1");
    }

    // SW-609: with the deck live for an audience, the phone's Join code puts the join bar on the room's screen and takes
    // it off again, and the presenter window's own toggle follows it.
    [Test]
    public async Task PhoneTogglesJoinCode_WhenTheDeckIsLive_ShouldShowAndHideTheJoinBarOnTheRoomsScreen()
    {
        var id = await CreateDeckAsync(TestUser.Sam, $"Phone join code {Guid.NewGuid():N}");
        var presenter = await OpenPresenterAsync(TestUser.Sam, id);
        var audience = await Talks.OpenAudienceAsync(Page, await Context.NewPageAsync(), presenter, id);
        await using var phone = await OpenPhoneAsync(TestUser.Sam);
        var speaker = new SpeakerPage(phone.Page);
        await speaker.GotoAsync(id);
        await speaker.IsLiveAsync();
        await Expect(speaker.Blackout).ToBeVisibleAsync();
        await Expect(speaker.JoinCode).ToHaveCountAsync(0);

        await presenter.Live.GoLive.ClickAsync();

        await Expect(speaker.JoinCode).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(audience.JoinBar(1)).ToHaveCountAsync(0);

        await speaker.JoinCode.ClickAsync();

        await Expect(audience.JoinBar(1)).ToBeVisibleAsync();
        await Expect(audience.JoinBarCode(1)).ToHaveTextAsync(await presenter.Live.Code.InnerTextAsync());
        await Expect(speaker.JoinCode).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(presenter.Live.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "true");

        await speaker.JoinCode.ClickAsync();

        await Expect(audience.JoinBar(1)).ToHaveCountAsync(0);
        await Expect(speaker.JoinCode).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(presenter.Live.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "false");
    }

    // A private deck of SLIDES quote slides, each with its own text, made the way an agent makes one.
    private static async Task<string> CreateDeckAsync(TestUser owner, string title)
    {
        string id;
        await using (var agent = await AgentSeed.ConnectAsync(owner))
        {
            id = (await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = title })).GetProperty("deck").GetString()!;
        }
        var quotes = Enumerable.Range(1, SLIDES)
            .Select(n => $"Point {n.ToString(CultureInfo.InvariantCulture)}: ship the smallest thing that teaches us something.")
            .ToArray();
        await AgentSeed.AddQuotesAsync(owner, id, quotes);
        return id;
    }

    // The laptop's presenter window, in the test's own context, signed in as the owner and joined.
    private async Task<PresenterPage> OpenPresenterAsync(TestUser owner, string deckId)
    {
        await SignIn.AsAsync(Page, owner);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLoadedAsync();
        await presenter.IsLiveAsync();
        return presenter;
    }

    // A phone: its own browser context at a phone viewport with touch, signed in as the owner.
    private async Task<Phone> OpenPhoneAsync(TestUser owner)
    {
        var options = ContextOptions();
        options.ViewportSize = new ViewportSize { Width = 390, Height = 844 };
        options.IsMobile = true;
        options.HasTouch = true;
        var context = await Browser.NewContextAsync(options);
        var page = await context.NewPageAsync();
        await SignIn.AsAsync(page, owner);
        return new Phone(context, page);
    }

    private static Regex SpeakerUrl(string deckId) => new($"/remote/{Regex.Escape(deckId)}$");

    [GeneratedRegex(@"^29:[0-5]\d left$")]
    private static partial Regex TimeLeftOnceRunning();

    [GeneratedRegex(@"^29:[0-5]\d left of 30:00$")]
    private static partial Regex PresenterTimeLeftOnceRunning();

    private sealed class Phone(IBrowserContext context, IPage page) : IAsyncDisposable
    {
        public IPage Page { get; } = page;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }
}
