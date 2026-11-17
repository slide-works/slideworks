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

    public async Task GotoAsync(string? deckId = null) => await page.GotoAsync(PathFor(deckId));

    // Waits until the pick page has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();
}
