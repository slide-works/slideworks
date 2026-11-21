import {
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import type { PollActivity } from '../../polls/types';
import { QA_TABS_TEST_IDS } from '../testIds';
import type { QaRoom } from '../types';
import { AskBar } from './AskBar';
import { AskSheet } from './AskSheet';
import { AskSheetContext, type AskSheet as AskSheetValue } from './AskSheetContext';
import { QuestionList } from './QuestionList';
import { ReactionRow } from './ReactionRow';
import { useQuestions } from './useQuestions';

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
  'inline-flex min-h-11 flex-1 items-center justify-center gap-1.5 rounded-[10px] text-[13px] font-semibold transition-colors aria-selected:bg-white aria-selected:text-(--sw-night) focus-visible:outline-2 focus-visible:outline-(--sw-accent-glow) motion-reduce:duration-0';

// The open poll in the audience state's activity, as one value per round, or null when no poll is open.
const openPollOf = (activity: PollActivity | null) =>
  activity?.type === 'poll' && activity.phase === 'open'
    ? `${activity.round ?? ''}/${activity.number}`
    : null;

// The guest page's two tabs at the bottom (Q8, D1): Slide, with the reaction row under the activity card, and Questions,
// with the list, the Ask bar and the ask sheet. It provides useAskSheet. With questions off there is no tab bar and only
// the Slide tab's content. In the landscape slide-only layout the Slide panel lays out as SW-5's page did (display:
// contents), the Questions panel, the tab bar and the reaction row hide, as the header does.
// The Questions tab names its count of open questions; the Slide tab carries a dot while a poll is open and Questions
// is showing. D7: a poll opening while Questions is showing switches to Slide, unless the ask sheet is open, when
// pollJustOpened is set until the sheet closes. A poll already open when the guest chose Questions doesn't pull them back.
export function GuestTabs({ tab, onTab, activity, room, presenter, children }: GuestTabsProps) {
  const { questions } = useQuestions();
  const [open, setOpen] = useState(false);
  const [pollJustOpened, setPollJustOpened] = useState(false);

  // D7, worked out while rendering so the switch lands before the paint: a new open poll, seen with Questions showing.
  const openPoll = openPollOf(activity);
  const [seenPoll, setSeenPoll] = useState(openPoll);
  const [switches, setSwitches] = useState(0);
  // What a screen reader hears when the switch happens: the hidden Questions panel can't say it. Cleared when the guest
  // chooses Questions again, so the next switch changes the text and is heard.
  const [switchNote, setSwitchNote] = useState('');
  const questionsPanel = useRef<HTMLDivElement>(null);
  const slideTabButton = useRef<HTMLButtonElement>(null);
  if (seenPoll !== openPoll) {
    setSeenPoll(openPoll);
    if (openPoll !== null && tab === 'questions') {
      if (open) setPollJustOpened(true);
      else {
        setSwitches((n) => n + 1);
        setSwitchNote('A poll just opened. Showing the Slide tab.');
      }
    }
  }
  // The tab is GuestQa's, so the switch is asked for once per poll opening, after this render and before the paint.
  const switched = useRef(0);
  useLayoutEffect(() => {
    if (switches === switched.current) return;
    switched.current = switches;
    // Focus inside the Questions panel would be lost to the page when the panel hides: move it to the Slide tab.
    if (questionsPanel.current?.contains(document.activeElement)) slideTabButton.current?.focus();
    onTab('slide');
  }, [switches, onTab]);

  const sheet = useMemo<AskSheetValue>(
    () => ({
      open,
      show: () => {
        setPollJustOpened(false);
        setOpen(true);
      },
      close: () => {
        setPollJustOpened(false);
        setOpen(false);
      },
      goToPoll: () => {
        setPollJustOpened(false);
        setOpen(false);
        onTab('slide');
      },
      pollJustOpened,
    }),
    [open, pollJustOpened, onTab],
  );

  const openCount = questions.filter((q) => q.state === 'open').length;
  const pollDot = openPoll !== null && tab === 'questions';

  const on = room !== null && room.questions !== 'off';
  // One tree whether questions are on or off, so SW-5's slide and SW-6's poll card (and a guest's unsent picks) are never
  // remounted when the presenter turns questions on or off. Off, the Slide panel is display: contents with no tab
  // semantics, so the page lays out exactly as SW-5's. Panels hide with classes, not the hidden attribute, so the
  // landscape slide-only layout can show the slide whichever tab is chosen.
  const slidePanel = on
    ? {
        id: 'qa-panel-slide',
        role: 'tabpanel',
        'aria-labelledby': 'qa-tab-slide',
        'data-testid': QA_TABS_TEST_IDS.slidePanel,
        className: `${tab === 'slide' ? 'flex' : 'hidden'} min-h-0 flex-1 flex-col gap-3 [@media(orientation:landscape)_and_(max-height:500px)]:contents`,
      }
    : { className: 'contents' };

  // The arrow keys, Home and End move between the two tabs and select the one they land on (the tablist pattern); only
  // the selected tab is a tab stop.
  const onTabKeys = (event: KeyboardEvent<HTMLElement>) => {
    const next =
      event.key === 'ArrowRight' || event.key === 'ArrowLeft'
        ? tab === 'slide'
          ? 'questions'
          : 'slide'
        : event.key === 'Home'
          ? 'slide'
          : event.key === 'End'
            ? 'questions'
            : null;
    if (next === null) return;
    event.preventDefault();
    if (next === 'questions') setSwitchNote('');
    onTab(next);
    event.currentTarget
      .querySelector<HTMLElement>(next === 'slide' ? '#qa-tab-slide' : '#qa-tab-questions')
      ?.focus();
  };

  return (
    <AskSheetContext value={sheet}>
      <div {...slidePanel}>
        {children}
        <ReactionRow presenter={presenter} />
      </div>
      <p role="status" className="sr-only">
        {switchNote}
      </p>
      {on && (
        <div
          ref={questionsPanel}
          id="qa-panel-questions"
          role="tabpanel"
          aria-labelledby="qa-tab-questions"
          data-testid={QA_TABS_TEST_IDS.questionsPanel}
          className={`${tab === 'questions' ? 'flex' : 'hidden'} relative min-h-0 flex-1 flex-col gap-3 [@media(orientation:landscape)_and_(max-height:500px)]:hidden`}
        >
          <QuestionList presenter={presenter} />
          <AskBar />
          <AskSheet presenter={presenter} />
        </div>
      )}
      {on && (
        <div
          role="tablist"
          aria-label="Guest page"
          data-testid={QA_TABS_TEST_IDS.bar}
          onKeyDown={onTabKeys}
          className="flex shrink-0 gap-1 rounded-[14px] bg-white/10 p-1 [@media(orientation:landscape)_and_(max-height:500px)]:hidden"
        >
          <button
            ref={slideTabButton}
            id="qa-tab-slide"
            type="button"
            role="tab"
            aria-selected={tab === 'slide'}
            tabIndex={tab === 'slide' ? 0 : -1}
            aria-controls="qa-panel-slide"
            data-testid={QA_TABS_TEST_IDS.slideTab}
            className={TAB_CLASS}
            onClick={() => onTab('slide')}
          >
            Slide
            {pollDot && (
              <span
                role="img"
                aria-label="A poll is open"
                data-testid={QA_TABS_TEST_IDS.slideDot}
                className="size-2 rounded-full bg-[#4ade80]"
              />
            )}
          </button>
          <button
            id="qa-tab-questions"
            type="button"
            role="tab"
            aria-selected={tab === 'questions'}
            tabIndex={tab === 'questions' ? 0 : -1}
            aria-controls="qa-panel-questions"
            data-testid={QA_TABS_TEST_IDS.questionsTab}
            className={TAB_CLASS}
            aria-label={openCount > 0 ? `Questions, ${openCount} open` : 'Questions'}
            onClick={() => {
              setSwitchNote('');
              onTab('questions');
            }}
          >
            Questions
            {openCount > 0 && (
              <span
                aria-hidden
                data-testid={QA_TABS_TEST_IDS.questionsBadge}
                className="rounded-full bg-(--sw-accent) px-1.5 py-[3px] text-[10.5px] leading-none font-bold text-white tabular-nums"
              >
                {openCount}
              </span>
            )}
          </button>
        </div>
      )}
    </AskSheetContext>
  );
}
