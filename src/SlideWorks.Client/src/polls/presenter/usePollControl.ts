import { createContext, useContext } from 'react';
import type { PollControlView } from '../types';

// The five things a presenter does to a poll (V4), each one poll route: open, close, reveal, show answer, new round.
export type PollAction = 'open' | 'close' | 'reveal' | 'showAnswer' | 'newRound';

// What the poll panel (presenter window) and the poll strip (phone remote) read (SW-6 V8). Frozen after wave 0.1: the
// real provider (PollControlLive) and the fixture provider (PollControlFixture) both give this shape.
export type PollControlValue = {
  // The poll on the presenter's slide, or null when that slide has no poll or it hasn't been read yet.
  control: PollControlView | null;
  // Each sends its route and settles once the answer is in. A refused or failed call never rejects: it sets error.
  open(): Promise<void>;
  close(): Promise<void>;
  reveal(): Promise<void>;
  showAnswer(): Promise<void>;
  newRound(): Promise<void>;
  // The message for the last refused or failed call ("not_live" shown as words), or null. Cleared by the next call.
  error: string | null;
};

// A provider's value, stamped with the deck it is for.
export type PollControlSource = { deckId: string; value: PollControlValue };

export const PollControlContext = createContext<PollControlSource | null>(null);

// The presenter's poll controls for deckId. Only under a PollControlLive or PollControlFixture for that deck: anywhere
// else is a wiring mistake, so it throws rather than draw controls that do nothing.
export function usePollControl(deckId: string): PollControlValue {
  const source = useContext(PollControlContext);
  if (source === null) throw new Error('usePollControl needs a poll control provider');
  if (source.deckId !== deckId)
    throw new Error(`usePollControl(${deckId}) is under the provider for deck ${source.deckId}`);
  return source.value;
}
