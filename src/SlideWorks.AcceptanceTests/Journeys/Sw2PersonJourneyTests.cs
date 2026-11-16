using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-2 journeys for a person: the owner changes the deck's look in the Look panel on the deck page, and the change
// reaches the API, the activity log and every open player over realtime. Decks are made with "+ New deck" (the default
// template and palette), slides go in through MCP add_slide, and a viewer's player runs in its own anonymous context on
// a link-shared deck. Palette values come from GET /api/looks, never from literals.
public partial class Sw2PersonJourneyTests : AcceptanceTest
{
    // A 1x1 PNG.
    private static readonly byte[] LOGO = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    // Criteria: changing the palette re-skins every slide; the Look panel offers the same palettes as set_look; every
    // look change writes one activity entry.
    [Test]
    public async Task PersonChangesPaletteAndEveryoneSeesIt_WhenOwnerPicksMidnight_ShouldRecolourAnOpenPlayer()
    {
        var id = await CreateLinkDeckWithAQuoteAsync($"Palette swap {Guid.NewGuid():N}");
        var looks = await LooksAsync();
        await using var viewer = await OpenPlayerAsync(id);
        await Expect(viewer.Player.Slide(1)).ToBeVisibleAsync();
        (await viewer.Player.AccentAsync(1)).ToLowerInvariant().ShouldBe(AccentOf(looks, DefaultPalette(looks)));

        var deck = new DeckPage(Page);
        await deck.GotoAsync(id);
        await deck.Look.ChoosePaletteAsync("midnight");

        await viewer.Player.WaitForAccentAsync(1, AccentOf(looks, "midnight"));
        await Expect(deck.Look.Palette).ToHaveValueAsync("midnight");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("changed the palette to Midnight");
    }

    // D9: a template switch keeps every slide; one whose layout is outside the new template gets a warning in the panel
    // and still renders in the player.
    [Test]
    public async Task PersonSwitchesTemplateAndOutsideLayoutsStillRender_WhenQuoteDeckMovesToUpdate_ShouldWarnAndStillPlayTheSlide()
    {
        var id = await CreateLinkDeckWithAQuoteAsync($"Template swap {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        await deck.GotoAsync(id);

        await deck.Look.ChooseTemplateAsync("update");

        await Expect(deck.Look.Warnings).ToHaveTextAsync(["Slide 1: layout 'quote' is not in the 'update' template"]);
        await Expect(deck.Look.Error).ToHaveCountAsync(0);
        await Expect(deck.ActivityRows.First).ToContainTextAsync("changed the template to update");

        var player = new PlayerPage(Page);
        await player.GotoAsync(id);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(player.Slide(1)).ToHaveAttributeAsync("data-layout", "quote");
        await Expect(player.Slide(1)).ToContainTextAsync("Small batches ship.");
    }

    // Criterion: the footer shows the deck's logo. The Look panel's logo field uploads the file to the same-origin
    // upload path, saves it with the look, and an open player shows it in the slide footer without a reload.
    [Test]
    public async Task PersonUploadsALogoAndItShowsInTheFooter_WhenOwnerPicksAPng_ShouldShowItInAnOpenPlayer()
    {
        var id = await CreateLinkDeckWithAQuoteAsync($"Logo {Guid.NewGuid():N}");
        await using var viewer = await OpenPlayerAsync(id);
        await Expect(viewer.Player.Slide(1)).ToBeVisibleAsync();
        await Expect(viewer.Player.Slide(1).GetByTestId("slide-logo")).ToHaveCountAsync(0);

        var deck = new DeckPage(Page);
        await deck.GotoAsync(id);
        await deck.Look.UploadLogoAsync("logo.png", "image/png", LOGO);

        await Expect(deck.Look.LogoError).ToHaveCountAsync(0);
        await Expect(deck.Look.LogoImage).ToBeVisibleAsync();
        var logo = viewer.Player.Slide(1).GetByTestId("slide-logo");
        await Expect(logo).ToBeVisibleAsync();
        await Expect(logo).ToHaveAttributeAsync("src", ImagePath());
        await Expect(logo).ToHaveJSPropertyAsync("complete", true);
        (await logo.EvaluateAsync<int>("e => e.naturalWidth")).ShouldBeGreaterThan(0);
    }

    // Signs in as Sam, makes a deck with "+ New deck", adds one quote slide through MCP and shares it by link with the
    // browser's own session, as the sharing dialog does.
    private async Task<string> CreateLinkDeckWithAQuoteAsync(string title)
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        var id = DeckPageUrl().Match(Page.Url).Groups["id"].Value;
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, "Small batches ship.");
        var response = await Page.APIRequest.PutAsync($"/api/decks/{id}/sharing", new APIRequestContextOptions
        {
            DataObject = new { level = "link", viewers = Array.Empty<string>() },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        return id;
    }

    // The catalogue the New deck dialog and the Look panel read (L6 looks.json).
    private async Task<JsonElement> LooksAsync()
    {
        var response = await Page.APIRequest.GetAsync("/api/looks");
        response.Status.ShouldBe(200, await response.TextAsync());
        return (await response.JsonAsync())!.Value;
    }

    private static string DefaultPalette(JsonElement looks) =>
        looks.GetProperty("palettes").EnumerateArray().Single(p => p.GetProperty("isDefault").GetBoolean()).GetProperty("name").GetString()!;

    private static string AccentOf(JsonElement looks, string palette) =>
        looks.GetProperty("palettes").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == palette)
            .GetProperty("tokens").GetProperty("accent").GetString()!.ToLowerInvariant();

    // A viewer's player on the deck, anonymous, in its own browser context.
    private async Task<Viewer> OpenPlayerAsync(string deckId)
    {
        var context = await Browser.NewContextAsync(ContextOptions());
        var player = new PlayerPage(await context.NewPageAsync());
        await player.GotoAsync(deckId);
        return new Viewer(context, player);
    }

    private sealed class Viewer(IBrowserContext context, PlayerPage player) : IAsyncDisposable
    {
        public PlayerPage Player { get; } = player;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();

    [GeneratedRegex(@"^/api/images/[^/]+$")]
    private static partial Regex ImagePath();
}
