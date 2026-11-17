using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The phone's speaker mode at /remote/{id} (P8): signed in, with no Shell chrome. The slide comes first, the notes
// underneath (D6), with Next and Previous buttons.
public sealed class SpeakerPage(IPage page)
{
    public static string PathFor(string deckId) => $"/remote/{deckId}";

    public ILocator Root => page.GetByTestId("speaker-page");

    public async Task GotoAsync(string deckId) => await page.GotoAsync(PathFor(deckId));

    // Waits until speaker mode has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();
}
