import type { ReactNode } from 'react';
import type { AudiencePolls, AudienceStatus } from '../../guest/useAudience';
import type { PollActivity } from '../types';
import { PollAnswerContext, type PollAnswer } from './usePollAnswer';

export type PollAnswerLiveProps = {
  // The join code: GET /api/join/{code}/poll and PUT /api/join/{code}/poll/answer (pollApi.ts).
  code: string;
  // The poll on screen, from the audience state. A new round, phase or roomSeesCounts means a fresh read.
  activity: PollActivity;
  // The audience connection's status: reconnecting or offline with an unsent pick is 'queued'.
  status: AudienceStatus;
  // The audience connection's poll side: every pollTally it receives (polls.onTally), and WatchPoll on that same
  // connection (polls.watchPoll), to call once the guest has answered an afterAnswer poll and again each time status
  // comes back to 'live' after a reconnect. A tally with null counts never clears counts already held for the round.
  polls: AudiencePolls;
  children: ReactNode;
};

const NO_ANSWER: PollAnswer = {
  picks: [],
  mine: null,
  tally: null,
  joinedLate: false,
  state: 'idle',
  error: null,
  toggle: () => {},
  send: () => {},
  clear: () => {},
};

// The guest's answer to the poll on screen, for the card under it (V6).
export function PollAnswerLive({ children }: PollAnswerLiveProps) {
  // TODO: read the guest's answer and the tally, send picks and follow the tally; until then the card shows the activity with no answer and a tap does nothing.
  return <PollAnswerContext value={NO_ANSWER}>{children}</PollAnswerContext>;
}
