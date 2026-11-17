using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The phone's pick page at /remote (P8): signed in, with no Shell chrome. It lists the person's live talks; the QR
// code's link, /remote?deck={id}, lands here and moves on to that deck's speaker mode.
public sealed class RemotePage(IPage page)
{
    public const string PATH = "/remote";

    // /remote, or /remote?deck={id} as the QR code encodes it.
    public static string PathFor(string? deckId = null) =>
        deckId is null ? PATH : $"{PATH}?deck={Uri.EscapeDataString(deckId)}";

    public ILocator Root => page.GetByTestId("remote-pick");

    public ILocator LiveCards => page.GetByTestId("remote-live-card");

    public ILocator Empty => page.GetByTestId("remote-empty");

    public ILocator Error => page.GetByTestId("remote-error");

    // The note shown when /remote?deck={id} names a talk that isn't live.
    public ILocator NotLive => page.GetByTestId("remote-not-live");

    // The card for the talk with this exact title.
    public ILocator CardTitled(string title) =>
        LiveCards.Filter(new() { Has = page.GetByTestId("remote-live-title").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(title)}$") }) });

    public static ILocator TitleOf(ILocator card) => card.GetByTestId("remote-live-title");

    public static ILocator SlideOf(ILocator card) => card.GetByTestId("remote-live-slide");

    public static ILocator WindowsOf(ILocator card) => card.GetByTestId("remote-live-window");

    public static ILocator ControlLinkOf(ILocator card) => card.GetByTestId("remote-live-control");

    public async Task GotoAsync(string? deckId = null) => await page.GotoAsync(PathFor(deckId));

    // Waits until the pick page has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();
}
