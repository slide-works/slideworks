import { createContext, useContext, type ReactNode } from 'react';
import type { PollControlView, PollPhase, PollPick } from '../polls/types';
import { useSlideNumber } from './slideNumber';

// What the poll slide draws live on the big screen (SW-6 V7). Frozen after wave 0.1: the real provider
// (PollScreenLive) and the fixture provider (PollScreenFixture) both give this shape.
export type PollScreen = {
  phase: PollPhase;
  // The round's number; in draft, the number it gets when opened.
  number: number;
  answered: number;
  // One count per option while the room sees them, else null: the slide then hatches the tracks and says when the
  // results show.
  counts: number[] | null;
  answerShown: boolean;
  // The 1-based correct option once the answer is shown, else null.
  correct: number | null;
  // How many picks a guest makes ("Poll · Pick up to 2"), and the note that percentages can add up to more than 100.
  pick: PollPick;
  max: number | null;
  answers?: 'choice' | 'text' | 'cloud';
  replies?: string[] | null;
  words?: { text: string; count: number }[] | null;
};

// What a provider gives: the live screen of one poll slide, the one the presenter is on, by its 1-based number. Every
// other slide reads null.
export type PollScreenSource = { slide: number; screen: PollScreen } | null;

const PollScreenContext = createContext<PollScreenSource>(null);

export function PollScreenProvider({
  source,
  children,
}: {
  source: PollScreenSource;
  children: ReactNode;
}) {
  return <PollScreenContext value={source}>{children}</PollScreenContext>;
}

// The live screen of the poll slide being rendered, or null for "no live data": the slide then draws the question, the
// options and empty tracks. Null outside a provider (the deck page, RenderPage, preview_deck, a viewer who can't
// present the deck), on a slide the provider isn't for, and outside a slide.
export function usePollScreen(): PollScreen | null {
  const source = useContext(PollScreenContext);
  const slide = useSlideNumber();
  return source !== null && source.slide === slide ? source.screen : null;
}

// The big screen's view of the presenter's poll view. Counts only while the room sees them, and the correct option
// only once the answer is shown: the room watches this screen.
export function pollScreenOf(control: PollControlView): PollScreenSource {
  return {
    slide: control.slide,
    screen: {
      phase: control.phase,
      number: control.number,
      answered: control.answered,
      counts: control.roomSeesCounts ? control.counts : null,
      answerShown: control.answerShown,
      correct: control.answerShown ? control.settings.correct : null,
      pick: control.settings.pick,
      max: control.settings.max,
      answers: control.settings.answers,
      replies: control.roomSeesCounts ? (control.replies ?? null) : null,
      words: control.roomSeesCounts ? (control.words ?? null) : null,
    },
  };
}
