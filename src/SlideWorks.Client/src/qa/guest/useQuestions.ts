import { createContext, useContext } from 'react';
import type { GuestQuestion, MyVotes, QaRoom } from '../types';

// Where the questions list is (Q8):
// - loading: from mount until the first GET lands. Nothing can be tapped.
// - ready: the list is held; it is kept through later re-reads.
// - failed: the first read failed. `error` says why.
export type QuestionsState = 'loading' | 'ready' | 'failed';

// Where the guest's question is:
// - idle: nothing in flight.
// - sending: a POST is in flight.
// - queued: offline or reconnecting with an unsent question, kept on the phone and sent once the connection is back.
// - limited: a 429 came back; retryIn counts down to 0, then it is idle again.
// - failed: a refusal (409 questions_closed, a 400 rule) or a failure; `error` says which, and the draft stays.
export type AskingState = 'idle' | 'sending' | 'queued' | 'limited' | 'failed';

// Which call the current error came from: the list's read, an ask or an upvote. The list shows a read's or an upvote's
// error; the ask sheet shows an ask's.
export type QuestionErrorFrom = 'read' | 'ask' | 'vote';

// How the list is ordered: top by votes (ties to the older question), new with the newest first.
export type QuestionSort = 'top' | 'new';

// The phone's questions (Q4, Q8). Frozen: G1 fills it in behind QuestionsLive, and the parts build against
// QuestionsFixture.
export type Questions = {
  // The room the list was read with, or the newer one from the audience state. Null before the first read.
  room: QaRoom | null;
  // In `number` order, as the server sends them; the list sorts. Each question's `votes` is the server's last count with
  // this guest's upvotes as `mine` shows them, toggles not yet sent applied, so a part shows it as is.
  questions: GuestQuestion[];
  // The guest's own upvotes in the round (picks are question numbers), with any toggle not yet sent applied.
  mine: MyVotes;
  state: QuestionsState;
  // Sends a question; name is null for an anonymous one. Resolves when the call has ended, whatever the outcome:
  // asking and error say how it went.
  ask(text: string, name: string | null): Promise<void>;
  asking: AskingState;
  // While limited: whole seconds until the guest may ask again, counting down. 0 otherwise.
  retryIn: number;
  // The latest failure's code or rule from the server ("questions_closed", "text: at most 200"), or the client's own
  // text for a call that never reached it. Null when nothing has failed since the last success.
  error: string | null;
  // The call `error` came from (Q8, amended in wave 1): null exactly when error is null.
  errorFrom: QuestionErrorFrom | null;
  // Adds or takes back this guest's upvote on question `number`.
  toggleVote(number: number): void;
  // Reads the list again: the failed state's "Try again".
  retry(): void;
  sort: QuestionSort;
  setSort(sort: QuestionSort): void;
};

// Holds the questions the provider above gives: QuestionsLive in the app, QuestionsFixture in specs.
export const QuestionsContext = createContext<Questions | null>(null);

// The phone's questions. The provider takes the code and the room, so every part reads the same hook under the live
// provider and the fixture.
export function useQuestions(): Questions {
  const questions = useContext(QuestionsContext);
  if (questions === null)
    throw new Error('useQuestions needs a QuestionsLive or a QuestionsFixture above it.');
  return questions;
}
