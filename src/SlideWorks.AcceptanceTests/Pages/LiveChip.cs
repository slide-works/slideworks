namespace SlideWorks.AcceptanceTests.Pages;

// The owner's live chip in the player's control bar (SW-5 J6): going live, the code, the joined count, the join bar
// toggle and ending. Anyone who is not the deck's owner has no chip, so Root has a count of 0 for them.
public sealed class LiveChip(IPage page)
{
    public ILocator Root => page.GetByTestId("live-chip");

    // Opens a session; shown while not live. Its name includes the deck title.
    public ILocator GoLive => Root.GetByTestId("live-chip-go-live");

    // Ends the session; shown while live. Its name includes the deck title.
    public ILocator End => Root.GetByTestId("live-chip-end");

    // The join code as the room reads it ("482 913"), shown while live.
    public ILocator Code => Root.GetByTestId("live-chip-code");

    // Turns the join bar on or off; shown while live. aria-pressed says which.
    public ILocator JoinBarToggle => Root.GetByTestId("live-chip-join-bar");

    // "N joined", shown while live.
    public ILocator Count => Root.GetByTestId("live-chip-count");

    // The message for a failed Go live, End or join bar change; empty when nothing failed.
    public ILocator Error => Root.GetByTestId("live-chip-error");
}
