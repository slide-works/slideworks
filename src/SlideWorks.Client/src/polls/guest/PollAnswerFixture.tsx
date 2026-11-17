import { useMemo, useState, type ReactNode } from 'react';
import type { MyAnswer, PollPick, PollTally } from '../types';
import { PollAnswerContext, type PollAnswer, type PollAnswerState } from './usePollAnswer';

export type PollAnswerFixtureProps = {
  // Every state of V6 is one prop away. Defaults: idle, nothing picked, no answer, no tally, no error.
  state?: PollAnswerState;
  picks?: number[];
  mine?: MyAnswer | null;
  tally?: PollTally | null;
  error?: string | null;
  // How toggle changes the picks on the phone: 'one' replaces them, 'upTo' adds or removes one, never past max.
  pick?: PollPick;
  max?: number | null;
  // Called with each toggle, send and clear, so a spec sees what the card asked for.
  onToggle?: (position: number) => void;
  onSend?: () => void;
  onClear?: () => void;
  children: ReactNode;
};

const toggled = (picks: number[], position: number, pick: PollPick, max: number | null) => {
  if (pick === 'one') return [position];
  if (picks.includes(position)) return picks.filter((p) => p !== position);
  if (max !== null && picks.length >= max) return picks;
  return [...picks, position].sort((a, b) => a - b);
};

// Component tests only: gives the card a PollAnswer in whatever state the spec names, with no network. toggle and clear
// change the picks on the phone the way the real hook does, so radios and checkboxes follow a tap; nothing else moves
// unless the spec mounts it again with other props. A new picks prop replaces the held picks.
export function PollAnswerFixture({
  state = 'idle',
  picks = [],
  mine = null,
  tally = null,
  error = null,
  pick = 'one',
  max = null,
  onToggle,
  onSend,
  onClear,
  children,
}: PollAnswerFixtureProps) {
  const [given, setGiven] = useState(picks);
  const [held, setHeld] = useState(picks);
  if (given !== picks && given.join() !== picks.join()) {
    setGiven(picks);
    setHeld(picks);
  }

  const value = useMemo<PollAnswer>(
    () => ({
      picks: held,
      mine,
      tally,
      state,
      error,
      toggle: (position) => {
        setHeld((current) => toggled(current, position, pick, max));
        onToggle?.(position);
      },
      send: () => onSend?.(),
      clear: () => {
        setHeld([]);
        onClear?.();
      },
    }),
    [held, mine, tally, state, error, pick, max, onToggle, onSend, onClear],
  );

  return <PollAnswerContext value={value}>{children}</PollAnswerContext>;
}
