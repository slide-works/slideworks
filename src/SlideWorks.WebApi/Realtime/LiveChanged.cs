namespace SlideWorks.WebApi.Realtime;

// The deck went live or its live session ended (J1, D16): a viewer re-reads the deck to see the join object's live flag.
public sealed record LiveChanged(string DeckId) : DeckEvent(DeckId);
