using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The presenter window at /decks/{id}/present (P8): signed in, with no Shell chrome. The deck page's Presenter view
// action opens it, and opens the audience window next to it.
public sealed class PresenterPage(IPage page)
{
    public static string PathFor(string deckId) => $"/decks/{deckId}/present";

    public ILocator Root => page.GetByTestId("presenter-page");

    public async Task GotoAsync(string deckId) => await page.GotoAsync(PathFor(deckId));

    // Waits until the presenter window has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();
}
