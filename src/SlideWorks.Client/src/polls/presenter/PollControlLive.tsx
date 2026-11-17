import type { ReactNode } from 'react';
import { PollControlContext, type PollControlSource } from './usePollControl';

export type PollControlLiveProps = {
  deckId: string;
  // The presenter's slide, 1-based, from SW-4's presentation state.
  slide: number;
  children: ReactNode;
};

const settled = () => Promise.resolve();

// The presenter's poll controls in the presenter window and on the phone remote (SW-6 V8). It watches the deck's
// audience, reads GET /api/decks/{id}/polls/{slide} when the slide or the deck changes, follows pollControl (dropping a
// lower seq), and sends the five actions through pollControlApi, showing a refused call's code as a message.
export function PollControlLive({ deckId, children }: PollControlLiveProps) {
  // TODO: no control yet: the panel and the strip show no poll. The real provider replaces this.
  const source: PollControlSource = {
    deckId,
    value: {
      control: null,
      open: settled,
      close: settled,
      reveal: settled,
      showAnswer: settled,
      newRound: settled,
      error: null,
    },
  };
  return <PollControlContext value={source}>{children}</PollControlContext>;
}
