import { useMemo, type ReactNode } from 'react';
import type { AudienceStatus } from '../../guest/useAudience';
import type { QaRoom } from '../types';
import { QuestionsContext, type Questions } from './useQuestions';

export type QuestionsLiveProps = {
  // The join code the page is on, for the questions' GET, POST and PUT.
  code: string;
  // The audience state's room: a new round or a higher listRevision means the list is read again.
  room: QaRoom | null;
  // The audience connection's status, so a question or an upvote waits while it is reconnecting or offline, and the list
  // is read again after a reconnect.
  status: AudienceStatus;
  // Whether the Questions tab is showing: while it is, the list is read again once a minute so the ages move.
  showing: boolean;
  children: ReactNode;
};

const noop = () => undefined;

// The phone's questions from the server (Q8), given to the parts through useQuestions. The questions channel is
// useAudienceQuestions(): subscribe to it before the first GET.
// TODO: a stub that stays loading; the real provider reads, asks, votes and follows questionTally.
export function QuestionsLive({ room, children }: QuestionsLiveProps) {
  const value = useMemo<Questions>(
    () => ({
      room,
      questions: [],
      mine: { picks: [], revision: 0 },
      state: 'loading',
      ask: async () => undefined,
      asking: 'idle',
      retryIn: 0,
      error: null,
      toggleVote: noop,
      retry: noop,
      sort: 'top',
      setSort: noop,
    }),
    [room],
  );
  return <QuestionsContext value={value}>{children}</QuestionsContext>;
}
