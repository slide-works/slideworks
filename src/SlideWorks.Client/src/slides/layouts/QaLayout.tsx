import {
  QA_SLIDE_TEST_IDS as IDS,
  qaSlideRowTestId,
  qaSlideRowTextTestId,
  qaSlideRowVotesTestId,
} from '../../qa/testIds';
import {
  useQaScreenState,
  type QaScreen,
  type QaScreenState,
} from '../../qa/screen/QaScreenContext';
import headingStyles from '../blocks/SlideHeading.module.css';
import { Kicker } from '../blocks/Kicker';
import { useJoin } from '../JoinContext';
import type { LayoutProps } from '../slots';
import { DEFAULT_QA_TITLE } from './qaTitle';
import styles from './QaLayout.module.css';

// What the empty state says, by phase.
const EMPTY = {
  open: ['No questions yet', 'Ask on your phone. The most upvoted questions show here.'],
  closed: ['Questions are closed', 'Thanks for asking.'],
} as const;

// The one-up question's size step by length: 80px for a normal question, smaller as the text grows (CSS).
function sizeOf(length: number): 'xl' | 'l' | 'm' | 's' {
  if (length <= 60) return 'xl';
  if (length <= 100) return 'l';
  if (length <= 140) return 'm';
  return 's';
}

function Body({ state }: { state: QaScreenState }) {
  if (state.kind === 'pending') return null;
  if (state.kind === 'off') {
    return (
      <div data-testid={IDS.off} className={styles.empty}>
        <b>Questions are off</b>
      </div>
    );
  }
  if (state.kind === 'none') {
    return (
      <div data-testid={IDS.notLive} className={styles.empty}>
        <b>Questions from the room appear here</b>
        <span>While the deck is live, the audience asks and upvotes on their phones.</span>
      </div>
    );
  }
  const { onScreen, top, phase } = state.screen;
  if (onScreen !== null) {
    return (
      <div data-testid={IDS.oneUp} className={styles.oneUp}>
        <p className={styles.oneUpLabel}>
          <em data-testid={IDS.oneUpVotes}>▲ {onScreen.votes}</em>
          <span data-testid={IDS.oneUpName}>{onScreen.name ?? 'Anonymous'}</span>
        </p>
        <p
          data-testid={IDS.oneUpText}
          className={styles.oneUpText}
          data-size={sizeOf(onScreen.text.length)}
        >
          {onScreen.text}
        </p>
      </div>
    );
  }
  if (top.length === 0) {
    const [title, text] = EMPTY[phase];
    return (
      <div data-testid={phase === 'closed' ? IDS.closed : IDS.empty} className={styles.empty}>
        <b>{title}</b>
        <span>{text}</span>
      </div>
    );
  }
  return (
    <ol data-testid={IDS.rows} className={styles.rows}>
      {top.map((row, i) => (
        <li
          key={row.number}
          data-testid={qaSlideRowTestId(row.number)}
          className={i === 0 ? `${styles.row} ${styles.first}` : styles.row}
        >
          <span data-testid={qaSlideRowVotesTestId(row.number)} className={styles.votes}>
            {row.votes}
          </span>
          <span data-testid={qaSlideRowTextTestId(row.number)} className={styles.text}>
            {row.text}
          </span>
        </li>
      ))}
    </ol>
  );
}

function footerNote(screen: QaScreen): string | null {
  if (screen.onScreen !== null)
    return `On screen now · ${Math.max(screen.total - 1, 0)} more waiting`;
  if (screen.total > 5) return `Top 5 of ${screen.total}, by upvotes`;
  // Closed, nobody can ask, so the footer has no invitation to.
  return screen.phase === 'open' ? 'Ask and upvote on your phone' : null;
}

// The Q&A slide (light, SW-9 Q1, Q9): its title over the room's top five open questions, from useQaScreen(), or one
// question at full size while the presenter has one on screen. It also draws the empty and closed states. With no live
// data (the deck page, RenderPage, preview_deck, a viewer who can't present, a deck that isn't live) it draws the "not
// live" state. The big-screen state (useQaScreenState) tells that from "off" (the room has no round: the title and
// "Questions are off") and "pending" (not read yet, or a phone: the title and a blank body). The join pill shows while
// the deck is live.
export function QaLayout({ slots }: LayoutProps<'qa'>) {
  const state = useQaScreenState();
  const joined = useJoin();
  const screen = state.kind === 'on' ? state.screen : null;
  const note = screen === null ? null : footerNote(screen);

  return (
    <div data-testid="layout-qa" className={`sw-layout ${styles.qa}`}>
      <div data-testid={IDS.slide} className="contents">
        {joined?.live ? (
          <p data-testid={IDS.join} className={styles.join}>
            {joined.join.hostLabel} · <b>{joined.join.codeLabel}</b>
          </p>
        ) : null}
        <Kicker testId={IDS.kicker} text="Q&A" />
        <h2 data-testid={IDS.title} className={headingStyles.headline}>
          {slots.title ?? DEFAULT_QA_TITLE}
        </h2>
        <div className={`sw-body ${styles.body}`}>
          <Body state={state} />
        </div>
        <div data-testid={IDS.footer} className={styles.foot}>
          {screen === null ? (
            state.kind === 'none' ? (
              <span data-testid={IDS.notLivePill} className={styles.state} data-phase="notLive">
                Not live
              </span>
            ) : null
          ) : (
            <>
              <span data-testid={IDS.phase} className={styles.state} data-phase={screen.phase}>
                {screen.phase === 'open' ? 'Open' : 'Closed'}
              </span>
              <span data-testid={IDS.total} className={styles.total}>
                <b>{screen.total}</b> {screen.total === 1 ? 'question' : 'questions'}
              </span>
              {note === null ? null : (
                <span data-testid={IDS.footerNote} className={styles.note}>
                  {note}
                </span>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
