namespace SlideWorks.AcceptanceTests.Pages;

// The deck page at /decks/{id}, inside the signed-in shell: the slide grid as thumbnails, the selected slide's notes
// and link, the activity log, Share and Present.
public sealed class DeckPage(IPage page)
{
    public static string PathFor(string deckId) => $"/decks/{deckId}";

    public ILocator Root => page.GetByTestId("deck-page");

    public ILocator Title => page.GetByTestId("deck-title");

    public ILocator SlideCount => page.GetByTestId("deck-slide-count");

    public ILocator SharingLevel => page.GetByTestId("deck-sharing-level");

    public ILocator Thumbnails => page.GetByTestId("deck-thumb");

    public ILocator SelectedNumber => page.GetByTestId("selected-number");

    public ILocator SelectedLayout => page.GetByTestId("selected-layout");

    public ILocator SelectedNotes => page.GetByTestId("selected-notes");

    public ILocator SelectedLink => page.GetByTestId("selected-link");

    public ILocator ActivityRows => page.GetByTestId("deck-activity").GetByTestId("activity-row");

    public ILocator Share => page.GetByTestId("deck-share");

    public ILocator Present => page.GetByTestId("deck-present");

    public ILocator SharingDialog => page.GetByTestId("sharing-dialog");

    public ILocator NotFound => page.GetByTestId("deck-not-found");

    public ILocator Error => page.GetByTestId("deck-error");

    // The Look panel, shown only to the deck's owner.
    public LookPanel Look => new(page);

    // The thumbnail of slide {number}, counted from 1.
    public ILocator Thumbnail(int number) => Thumbnails.Nth(number - 1);

    public async Task GotoAsync(string deckId) => await page.GotoAsync(PathFor(deckId));

    // Selects slide {number} in the grid, so its notes and link show beside it.
    public async Task SelectSlideAsync(int number) =>
        await Thumbnail(number).GetByTestId("deck-thumb-select").ClickAsync();
}
