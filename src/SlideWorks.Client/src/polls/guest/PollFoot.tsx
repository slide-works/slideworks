import type { MyAnswer, PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';
import type { PollAnswerState } from './usePollAnswer';

export type PollFootProps = {
  // The phase, pick, max and results setting decide what the foot says.
  activity: PollActivity;
  // The presenter's name, for "Results show … when <presenter> closes the poll" and "<presenter> hasn't opened this
  // poll yet".
  presenter: string;
  // What is selected on the phone, for "1 of 2 picked" and Send.
  picks: number[];
  // The answer the server holds, or null: "Answer sent", "You didn't send an answer for this one".
  mine: MyAnswer | null;
  // How many have answered so far, from the latest tally, or null before one arrives. Always shown (D21).
  answered: number | null;
  // Closed with no answer: true for "You joined after this poll closed", false for "You didn't send an answer".
  joinedLate: boolean;
  // Never 'conflict' or 'queued': PollConflict and PollQueued take the foot's place then. 'loading' says nothing yet.
  state: PollAnswerState;
  // A failed call's message, when state is 'failed'.
  error: string | null;
  // Pick several's Send, and sending again after a failure.
  onSend: () => void;
  // Pick several's "Change my answer": clears the picks on the phone to choose again.
  onClear: () => void;
};

// What the card says under the options: the hint, Send, sent, the answer count, closed, not open yet, a failure.
export function PollFoot({ error }: PollFootProps) {
  // TODO: draw every line of the foot; until then it shows only a failed call's message.
  return (
    <div data-testid={POLL_CARD_TEST_IDS.foot}>
      {error !== null && (
        <p data-testid={POLL_CARD_TEST_IDS.failed} role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
