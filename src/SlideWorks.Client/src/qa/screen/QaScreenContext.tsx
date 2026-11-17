import { createContext, useContext, type ReactNode } from 'react';
import type { QaControlView, QaShowOn } from '../types';

// The most upvoted open questions the Q&A slide lists.
const TOP = 5;

// One row of the Q&A slide's list.
export type QaScreenRow = { number: number; text: string; votes: number };

// The question the presenter put on screen. name is null for an anonymous question.
export type QaScreenQuestion = { number: number; text: string; name: string | null; votes: number };

// What the big screen draws from the room's questions (SW-9 Q9): the Q&A slide and the question card over any slide.
// Frozen after wave 0.2: the real provider (QaScreenLive) and the fixture provider (QaScreenFixture) both give this
// shape.
export type QaScreen = {
  // closed: nothing new can be asked; the list and the upvotes stay.
  phase: 'open' | 'closed';
  // How many open questions the room can see ("6 questions").
  total: number;
  // The five most upvoted open questions, ties to the older one.
  top: QaScreenRow[];
  onScreen: QaScreenQuestion | null;
  // Where a shown question appears: over any slide, or only on a Q&A slide.
  showOn: QaShowOn;
};

const QaScreenContext = createContext<QaScreen | null>(null);

export function QaScreenProvider({
  screen,
  children,
}: {
  screen: QaScreen | null;
  children: ReactNode;
}) {
  return <QaScreenContext value={screen}>{children}</QaScreenContext>;
}

// The room's questions on the big screen, or null for "no live data": the Q&A slide then draws its "not live" state and
// the card layer draws nothing. Null outside a provider (the deck page, RenderPage, preview_deck, thumbnails), for a
// viewer who can't present the deck, while the deck isn't live and while questions are off.
export function useQaScreen(): QaScreen | null {
  return useContext(QaScreenContext);
}

// The big screen's view of the presenter's view. Only what the room may see: no hidden or waiting question, and no
// answered one in the list. Null while not live or with no round (questions off).
export function qaScreenOf(control: QaControlView): QaScreen | null {
  if (!control.live || control.round === null) return null;
  const shown = control.questions.find((q) => q.number === control.onScreen) ?? null;
  return {
    phase: control.settings.questions === 'closed' ? 'closed' : 'open',
    total: control.counts.open,
    top: control.questions
      .filter((q) => q.state === 'open' && !q.hidden)
      .sort((a, b) => b.votes - a.votes || a.number - b.number)
      .slice(0, TOP)
      .map(({ number, text, votes }) => ({ number, text, votes })),
    onScreen:
      shown === null
        ? null
        : { number: shown.number, text: shown.text, name: shown.name, votes: shown.votes },
    showOn: control.settings.showOn,
  };
}
