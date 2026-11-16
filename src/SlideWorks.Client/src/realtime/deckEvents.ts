// Mirrors src/SlideWorks.WebApi/Realtime/DeckEvent.cs: every event names its deck and has a "type" discriminator.
// Add an event here when one is added there.

/** The deck's content changed: re-read the deck. */
export interface DeckChanged {
  type: 'deckChanged';
  deckId: string;
}

export type DeckEvent = DeckChanged;
