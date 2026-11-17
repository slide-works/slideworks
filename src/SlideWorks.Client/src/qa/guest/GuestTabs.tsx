import { useMemo, useState, type ReactNode } from 'react';
import type { PollActivity } from '../../polls/types';
import { QA_TABS_TEST_IDS } from '../testIds';
import type { QaRoom } from '../types';
import { AskBar } from './AskBar';
import { AskSheet } from './AskSheet';
import { AskSheetContext, type AskSheet as AskSheetValue } from './AskSheetContext';
import { QuestionList } from './QuestionList';
import { ReactionRow } from './ReactionRow';

export type GuestTab = 'slide' | 'questions';

export type GuestTabsProps = {
  // The tab showing, held by GuestQa so the questions provider knows when Questions is showing.
  tab: GuestTab;
  onTab(tab: GuestTab): void;
  // The audience state's activity: a poll opening while Questions is showing switches to Slide (D7).
  activity: PollActivity | null;
  // The audience state's room: no tab bar when it is null or its questions are off.
  room: QaRoom | null;
  presenter: string;
  // The Slide tab: SW-5's slide, progress and activity card.
  children: ReactNode;
};

const TAB_CLASS =
  'min-h-11 flex-1 rounded-[10px] text-[13px] font-semibold aria-selected:bg-white aria-selected:text-(--sw-night) focus-visible:outline-2 focus-visible:outline-(--sw-accent-glow)';

// The guest page's two tabs at the bottom (Q8, D1): Slide, with the reaction row under the activity card, and Questions,
// with the list, the Ask bar and the ask sheet. It provides useAskSheet. With questions off there is no tab bar and only
// the Slide tab's content. In the landscape slide-only layout the Slide panel lays out as SW-5's page did (display:
// contents) and the tab bar hides, as the header does.
// TODO: a shell with two plain tabs that switch; the tabs' badge, poll dot and the poll switch (D7) replace it.
export function GuestTabs({ tab, onTab, room, presenter, children }: GuestTabsProps) {
  const [open, setOpen] = useState(false);
  const sheet = useMemo<AskSheetValue>(
    () => ({
      open,
      show: () => setOpen(true),
      close: () => setOpen(false),
      goToPoll: () => {
        setOpen(false);
        onTab('slide');
      },
      pollJustOpened: false,
    }),
    [open, onTab],
  );

  if (room === null || room.questions === 'off')
    return (
      <>
        {children}
        <ReactionRow />
      </>
    );

  return (
    <AskSheetContext value={sheet}>
      <div
        id="qa-panel-slide"
        role="tabpanel"
        aria-labelledby="qa-tab-slide"
        data-testid={QA_TABS_TEST_IDS.slidePanel}
        hidden={tab !== 'slide'}
        className="flex min-h-0 flex-1 flex-col gap-3 [@media(orientation:landscape)_and_(max-height:500px)]:contents"
      >
        {children}
        <ReactionRow />
      </div>
      <div
        id="qa-panel-questions"
        role="tabpanel"
        aria-labelledby="qa-tab-questions"
        data-testid={QA_TABS_TEST_IDS.questionsPanel}
        hidden={tab !== 'questions'}
        className="relative flex min-h-0 flex-1 flex-col gap-3"
      >
        <QuestionList presenter={presenter} />
        <AskBar />
        <AskSheet presenter={presenter} />
      </div>
      <div
        role="tablist"
        aria-label="Guest page"
        data-testid={QA_TABS_TEST_IDS.bar}
        className="flex shrink-0 gap-1 rounded-[14px] bg-white/10 p-1 [@media(orientation:landscape)_and_(max-height:500px)]:hidden"
      >
        <button
          id="qa-tab-slide"
          type="button"
          role="tab"
          aria-selected={tab === 'slide'}
          aria-controls="qa-panel-slide"
          data-testid={QA_TABS_TEST_IDS.slideTab}
          className={TAB_CLASS}
          onClick={() => onTab('slide')}
        >
          Slide
        </button>
        <button
          id="qa-tab-questions"
          type="button"
          role="tab"
          aria-selected={tab === 'questions'}
          aria-controls="qa-panel-questions"
          data-testid={QA_TABS_TEST_IDS.questionsTab}
          className={TAB_CLASS}
          onClick={() => onTab('questions')}
        >
          Questions
        </button>
      </div>
    </AskSheetContext>
  );
}
