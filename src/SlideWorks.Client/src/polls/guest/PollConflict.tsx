import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';

export type PollConflictProps = {
  // The poll as it is now: its round number for "<presenter> started round 2".
  activity: PollActivity;
  presenter: string;
  // Send it: sends the kept pick again.
  onSend: () => void;
  // Clear: drops the kept pick.
  onClear: () => void;
};

// A 409 came back: the answer went against an old round or revision. The pick is kept and the card asks again. Focus
// moves to the message.
export function PollConflict({ presenter }: PollConflictProps) {
  // TODO: say what changed and offer Send it and Clear; until then it says only that the poll changed.
  return (
    <div data-testid={POLL_CARD_TEST_IDS.conflict} role="alert">
      {`${presenter} changed the poll. Your pick is kept.`}
    </div>
  );
}
