import type { PollActivity } from '../types';
import { PollCard } from './PollCard';
import { PollAnswerContext, type PollAnswer } from './usePollAnswer';

// Component tests only: a pick-several poll the guest has answered with option 1, whose clear() leaves the picks alone,
// as the real hook's does once an answer is sent. PollAnswerFixture's clear() empties them, which hides what "Change
// my answer" does to the card. It must live in its own file, because a component test cannot mount a component that is
// defined in the spec.
export function PollAnswerKept({
  activity,
  revision,
}: {
  activity: PollActivity;
  revision: number;
}) {
  const value: PollAnswer = {
    picks: [1],
    mine: { picks: [1], revision },
    tally: null,
    joinedLate: false,
    state: 'idle',
    error: null,
    toggle: () => {},
    send: () => {},
    clear: () => {},
  };
  return (
    <PollAnswerContext value={value}>
      <PollCard activity={activity} presenter="Sam" />
    </PollAnswerContext>
  );
}
