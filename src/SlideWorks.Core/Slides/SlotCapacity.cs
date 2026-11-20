namespace SlideWorks.Core.Slides;

// How many items of a typical length a list or table slot fits, from the same fit check add_slide runs.
// Fits is that count with every other list at its minimum. ItemPx is a rough height of one such item.
public sealed record SlotCapacity(
    string Slot,
    int Fits,
    int ItemPx,
    int BudgetPx,
    IReadOnlyList<string> Shares,
    string Note);
