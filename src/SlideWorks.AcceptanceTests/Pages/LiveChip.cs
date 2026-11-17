namespace SlideWorks.AcceptanceTests.Pages;

// The owner's live chip in the player's control bar (SW-5 J6): going live, the joined count and ending. Anyone who
// is not the deck's owner has no chip, so Root has a count of 0 for them.
public sealed class LiveChip(IPage page)
{
    public ILocator Root => page.GetByTestId("live-chip");

    // Goes live when the session is not open and ends it when it is; its name says which, and for which deck.
    public ILocator Toggle => Root.GetByTestId("live-chip-toggle");

    // The number who have joined, shown while live.
    public ILocator Count => Root.GetByTestId("live-chip-count");
}
