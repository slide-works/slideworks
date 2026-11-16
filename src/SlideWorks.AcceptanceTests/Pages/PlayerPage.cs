namespace SlideWorks.AcceptanceTests.Pages;

// The player at /d/{id}/{slug}, open to anonymous viewers. /d/{id} (or an old slug) moves to the deck's own slug,
// keeping the #slide.step hash.
public sealed class PlayerPage(IPage page)
{
    public static string PathFor(string deckId, string? slug = null) =>
        slug is null ? $"/d/{deckId}" : $"/d/{deckId}/{slug}";

    public ILocator Root => page.GetByTestId("player-page");

    public ILocator Player => page.GetByTestId("player");

    public ILocator NotFound => page.GetByTestId("player-not-found");

    public ILocator SignIn => page.GetByTestId("player-sign-in");

    public ILocator Error => page.GetByTestId("player-error");

    // Slide {number}, counted from 1. Every slide stays rendered; only the current one is visible.
    public ILocator Slide(int number) => page.GetByTestId($"slide-{number}");

    public async Task GotoAsync(string deckId, string? slug = null, string hash = "") =>
        await page.GotoAsync(PathFor(deckId, slug) + hash);
}
