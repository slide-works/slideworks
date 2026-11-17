// Mirrors src/SlideWorks.WebApi/Realtime/DeckEvent.cs: every event names its deck and has a "type" discriminator.
// Add an event here when one is added there.

/** The deck's content changed: re-read the deck. */
export interface DeckChanged {
  type: 'deckChanged';
  deckId: string;
}

/** A live session opened or ended: re-read, so the join slide and the join bar show the deck's live state (SW-5 D16). */
export interface LiveChanged {
  type: 'liveChanged';
  deckId: string;
}

export type DeckEvent = DeckChanged | LiveChanged;
