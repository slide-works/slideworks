import { createContext, useContext } from 'react';
import type { MyAnswer, PollTally } from '../types';

// Where the phone's answer is (V6):
// - loading: from mount until the first GET lands, and after a reconnect until the re-read lands. The card shows the
//   question and options but nothing can be tapped, so a guest who already answered never sees a tappable, unanswered
//   card.
// - idle: nothing in flight. With `mine` set, the answer is sent.
// - sending: a PUT is in flight.
// - queued: offline or reconnecting with an unsent pick. It sends when the connection is back, if the round is still
//   open.
// - conflict: a 409 came back. The pick is kept, and the card asks again.
// - failed: any other refusal or failure. `error` says which, so nothing fails silently.
export type PollAnswerState = 'loading' | 'idle' | 'sending' | 'queued' | 'conflict' | 'failed';

// The guest's answer to the poll on screen (V6). Frozen: G1a fills it in behind PollAnswerLive, and the card's parts
// build against PollAnswerFixture.
export type PollAnswer = {
  // What is selected on the phone: 0-based positions, ascending. After a send it equals mine.picks.
  picks: number[];
  // The answer the server holds for this guest in the current round, or null before the first send.
  mine: MyAnswer | null;
  // The latest tally for the current round. Its counts are null while this guest may not see them, and a tally with
  // null counts never clears counts already held for the round.
  tally: PollTally | null;
  // From GuestPollView.joinedLate: the round is closed, this guest has no answer in it and joined after it closed.
  joinedLate: boolean;
  state: PollAnswerState;
  // When state is 'failed': the refusal's code or rule from the server ("too_many_answers", "picks: at most 2"), or
  // the client's own text for a call that never reached it. Null in every other state.
  error: string | null;
  // Pick one: selects the option and sends at once (D3). Pick several: selects or unselects it, up to max, and sends
  // nothing.
  toggle(position: number): void;
  // Sends the picks: pick several's Send, and Send it after a conflict or a failure.
  send(): void;
  // Clears the picks on the phone without sending: Clear after a conflict.
  clear(): void;
};

// Holds the answer the provider above the card gives: PollAnswerLive in the app, PollAnswerFixture in specs.
export const PollAnswerContext = createContext<PollAnswer | null>(null);

// The guest's answer to the poll on screen. The code and the activity go to the provider (PollAnswerLive), so the card
// reads the same hook under the live provider and the fixture.
export function usePollAnswer(): PollAnswer {
  const answer = useContext(PollAnswerContext);
  if (answer === null)
    throw new Error('usePollAnswer needs a PollAnswerLive or a PollAnswerFixture above it.');
  return answer;
}
