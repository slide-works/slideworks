import { useMemo, type ReactNode } from 'react';
import type { PollControlView } from '../types';
import { PollControlContext, type PollAction, type PollControlSource } from './usePollControl';

// Tests only: provides the presenter's poll controls with a fixed view and error, as PollControlLive does once it has
// read them. Build `control` from pollControlView() in test-support/fixtures/pollStates.ts ("draft", "open" or
// "closed", with overrides for revealed, answer shown, earlier rounds or settings); null is a slide with no poll. Each
// action calls onAction with its name and changes nothing: mount again (or update) for the next state. Frozen after
// wave 0.1.
export function PollControlFixture({
  deckId,
  control,
  error = null,
  onAction,
  children,
}: {
  deckId: string;
  control: PollControlView | null;
  error?: string | null;
  onAction?: (action: PollAction) => void;
  children: ReactNode;
}) {
  const source = useMemo<PollControlSource>(() => {
    const act = (action: PollAction) => () => {
      onAction?.(action);
      return Promise.resolve();
    };
    return {
      deckId,
      value: {
        control,
        open: act('open'),
        close: act('close'),
        reveal: act('reveal'),
        showAnswer: act('showAnswer'),
        newRound: act('newRound'),
        error,
      },
    };
  }, [deckId, control, error, onAction]);
  return <PollControlContext value={source}>{children}</PollControlContext>;
}
