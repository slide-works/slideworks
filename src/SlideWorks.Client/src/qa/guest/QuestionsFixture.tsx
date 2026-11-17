import { useMemo, useState, type ReactNode } from 'react';
import type { GuestQuestion, MyVotes, QaRoom } from '../types';
import {
  QuestionsContext,
  type AskingState,
  type QuestionSort,
  type Questions,
  type QuestionsState,
} from './useQuestions';

export type QuestionsFixtureProps = {
  // Every state of Q8 is one prop away. Defaults: ready, the open room, no questions, no upvotes, idle, no error.
  room?: QaRoom | null;
  questions?: GuestQuestion[];
  mine?: MyVotes;
  state?: QuestionsState;
  asking?: AskingState;
  retryIn?: number;
  error?: string | null;
  sort?: QuestionSort;
  // Called with each ask, toggleVote, retry and setSort, so a spec sees what the parts asked for.
  onAsk?: (text: string, name: string | null) => void;
  onToggleVote?: (number: number) => void;
  onRetry?: () => void;
  onSort?: (sort: QuestionSort) => void;
  children: ReactNode;
};

const OPEN_ROOM: QaRoom = {
  round: '6f1d2c3b-4a5e-4f60-8a7b-9c0d1e2f3a4b',
  questions: 'open',
  names: true,
  reactions: true,
  listRevision: 1,
};

const NO_VOTES: MyVotes = { picks: [], revision: 0 };

// Component tests only: gives the parts a Questions value in whatever state the spec names, with no network. toggleVote
// adds or takes back the pick on the phone and setSort changes the sort, the way the real hook does, so a button follows
// a tap; nothing else moves unless the spec mounts it again with other props. ask resolves at once and changes nothing.
export function QuestionsFixture({
  room = OPEN_ROOM,
  questions = [],
  mine = NO_VOTES,
  state = 'ready',
  asking = 'idle',
  retryIn = 0,
  error = null,
  sort = 'top',
  onAsk,
  onToggleVote,
  onRetry,
  onSort,
  children,
}: QuestionsFixtureProps) {
  const [picks, setPicks] = useState(mine.picks);
  const [heldSort, setHeldSort] = useState(sort);

  const value = useMemo<Questions>(
    () => ({
      room,
      questions,
      mine: { picks, revision: mine.revision },
      state,
      asking,
      retryIn,
      error,
      sort: heldSort,
      ask: async (text, name) => onAsk?.(text, name),
      toggleVote: (number) => {
        setPicks((held) =>
          held.includes(number)
            ? held.filter((n) => n !== number)
            : [...held, number].sort((a, b) => a - b),
        );
        onToggleVote?.(number);
      },
      retry: () => onRetry?.(),
      setSort: (next) => {
        setHeldSort(next);
        onSort?.(next);
      },
    }),
    [
      room,
      questions,
      picks,
      mine.revision,
      state,
      asking,
      retryIn,
      error,
      heldSort,
      onAsk,
      onToggleVote,
      onRetry,
      onSort,
    ],
  );

  return <QuestionsContext value={value}>{children}</QuestionsContext>;
}
