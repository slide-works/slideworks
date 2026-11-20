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

// Why the big screen has, or hasn't, a list to draw (SW-9 Q9, addendum 3):
// none: not a presenter's screen, not live, or no provider (the Q&A slide draws "not live");
// pending: a presenter's screen whose first read hasn't landed, or a guest's phone, which draws no list (a blank body);
// off: read, and the room has no round, so questions are off ("Questions are off");
// on: the room's questions.
export type QaScreenState =
  { kind: 'none' } | { kind: 'pending' } | { kind: 'off' } | { kind: 'on'; screen: QaScreen };

const NONE: QaScreenState = { kind: 'none' };
export const QA_SCREEN_PENDING: QaScreenState = { kind: 'pending' };
export const QA_SCREEN_OFF: QaScreenState = { kind: 'off' };

const QaScreenContext = createContext<QaScreenState>(NONE);

// Give either a state, or (the original shape, kept for the layers' fixtures) a screen: null is none.
export function QaScreenProvider({
  screen,
  state,
  children,
}: {
  screen?: QaScreen | null;
  state?: QaScreenState;
  children: ReactNode;
}) {
  const value: QaScreenState =
    state ?? (screen === undefined || screen === null ? NONE : { kind: 'on', screen });
  return <QaScreenContext value={value}>{children}</QaScreenContext>;
}

// The room's questions on the big screen, or null for "no list to draw": the Q&A slide then draws its "not live", "off"
// or blank state and the card layer draws nothing. Null outside a provider (the deck page, RenderPage, preview_deck,
// thumbnails), for a viewer who can't present the deck, while the deck isn't live and while questions are off.
export function useQaScreen(): QaScreen | null {
  const state = useContext(QaScreenContext);
  return state.kind === 'on' ? state.screen : null;
}

// Which of the four states the big screen is in; the Q&A slide uses it to tell "off" from "not read yet" from "not live".
export function useQaScreenState(): QaScreenState {
  return useContext(QaScreenContext);
}

// The state a presenter's view gives once it has been read: none while not live, off with no round, else on.
export function qaScreenStateOf(control: QaControlView): QaScreenState {
  if (!control.live) return NONE;
  const screen = qaScreenOf(control);
  return screen === null ? QA_SCREEN_OFF : { kind: 'on', screen };
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
