namespace SlideWorks.WebApi.Realtime;

// The deck's content changed (a slide added, edited, moved or removed, its notes or its settings): a viewer re-reads
// the deck. It carries no detail on purpose, so a viewer never applies a partial change it could get wrong.
public sealed record DeckChanged(Guid DeckId) : DeckEvent(DeckId);
