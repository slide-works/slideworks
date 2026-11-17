namespace SlideWorks.AcceptanceTests.Pages;

// The questions strip on the presenter's phone remote (SW-9 Q9), under SW-6's poll strip, and the sheet its "All"
// button opens. One member for every id in src/SlideWorks.Client/src/qa/testIds.ts under QA_STRIP_TEST_IDS and the
// sheet's per-row and per-action ids; add one there and here together. Per-question members take the question's
// number (1-based). The sheet's members look on the page, since a sheet may sit outside the strip.
public sealed class QaStrip(IPage page)
{
    public ILocator Root => page.GetByTestId("qa-strip");

    // "On screen", when the strip's question is on screen.
    public ILocator OnScreen => Root.GetByTestId("qa-strip-on-screen");

    // "14"
    public ILocator Votes => Root.GetByTestId("qa-strip-votes");

    // "votes · 3 open · 1 to review"
    public ILocator Counts => Root.GetByTestId("qa-strip-counts");

    // The question's text, two lines at most.
    public ILocator Text => Root.GetByTestId("qa-strip-text");

    // "No open questions"
    public ILocator Empty => Root.GetByTestId("qa-strip-empty");

    // "Show", on a question that isn't on screen.
    public ILocator Show => Root.GetByTestId("qa-strip-show");

    // "Take off", on the question on screen.
    public ILocator TakeOff => Root.GetByTestId("qa-strip-take-off");

    // "Answered"
    public ILocator Answer => Root.GetByTestId("qa-strip-answer");

    // "All 6": opens the sheet.
    public ILocator All => Root.GetByTestId("qa-strip-all");

    // A refused action's message; absent (count 0) when nothing was refused.
    public ILocator Error => Root.GetByTestId("qa-strip-error");

    // The sheet with the open and to-review questions.
    public ILocator Sheet => page.GetByTestId("qa-strip-sheet");

    public ILocator SheetClose => page.GetByTestId("qa-strip-sheet-close");

    public ILocator SheetRows => page.GetByTestId("qa-strip-sheet-rows");

    // A refused action's message in the sheet.
    public ILocator SheetError => page.GetByTestId("qa-strip-sheet-error");

    // Question {number}'s row in the sheet, and its buttons, as the panel's rows.
    public ILocator SheetRow(int number) => page.GetByTestId($"qa-strip-sheet-row-{number}");

    public ILocator SheetApprove(int number) => SheetAction("approve", number);

    public ILocator SheetHide(int number) => SheetAction("hide", number);

    public ILocator SheetRestore(int number) => SheetAction("restore", number);

    public ILocator SheetAnswer(int number) => SheetAction("answer", number);

    public ILocator SheetReopen(int number) => SheetAction("reopen", number);

    public ILocator SheetShow(int number) => SheetAction("show", number);

    public ILocator SheetUnshow(int number) => SheetAction("unshow", number);

    private ILocator SheetAction(string action, int number) => page.GetByTestId($"qa-strip-sheet-{action}-{number}");
}
