namespace SlideWorks.AcceptanceTests.Pages;

// The poll strip on the presenter's phone remote (SW-6 V8), between the slide and Next. Absent (Root has a count of 0)
// on a slide with no poll. One member for every id in src/SlideWorks.Client/src/polls/testIds.ts (POLL_STRIP_TEST_IDS);
// add one there and here together.
public sealed class PollStrip(IPage page)
{
    public ILocator Root => page.GetByTestId("poll-strip");

    // "Open", "Closed" or "Draft"
    public ILocator State => Root.GetByTestId("poll-strip-state");

    // The answer count.
    public ILocator Answered => Root.GetByTestId("poll-strip-answered");

    // "93% of 41"
    public ILocator Rate => Root.GetByTestId("poll-strip-rate");

    // The controls; each is present only in the phases that allow it. Their names include the question.
    public ILocator Open => Root.GetByTestId("poll-strip-open");

    public ILocator Close => Root.GetByTestId("poll-strip-close");

    public ILocator Reveal => Root.GetByTestId("poll-strip-reveal");

    public ILocator ShowAnswer => Root.GetByTestId("poll-strip-show-answer");

    public ILocator NewRound => Root.GetByTestId("poll-strip-new-round");

    // A refused call's message; absent (count 0) when nothing was refused.
    public ILocator Error => Root.GetByTestId("poll-strip-error");
}
