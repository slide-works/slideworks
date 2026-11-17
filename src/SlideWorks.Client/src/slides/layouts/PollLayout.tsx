import { useRef } from 'react';
import { POLL_SLIDE_TEST_IDS } from '../../polls/testIds';
import { Kicker } from '../blocks/Kicker';
import { useJoin } from '../JoinContext';
import { usePollScreen, type PollScreen } from '../PollContext';
import { useMarkPollSlide } from '../PollScreenLive';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import { PollBar } from './PollBar';
import styles from './PollLayout.module.css';

// Up to 120 characters on at most two lines.
const QUESTION_FIT: FitBudget = { start: 60, floor: 40, lines: 2, lineHeight: 1.12 };

const PHASE_LABEL = { draft: 'Not open yet', open: 'Open', closed: 'Closed' } as const;

function kindOf(screen: PollScreen | null): string {
  if (screen === null) return 'Poll';
  return screen.pick === 'upTo' && screen.max !== null
    ? `Poll · Pick up to ${screen.max}`
    : 'Poll · Pick one';
}

// Fewer options get bigger rows; 7 or 8 options of 60 characters still fit the content box.
function densityOf(options: number): 'roomy' | 'medium' | 'dense' {
  return options <= 4 ? 'roomy' : options <= 6 ? 'medium' : 'dense';
}

// The share of people who answered that picked each option, as whole percentages (they add up to more than 100 when
// people pick several), or null when the slide draws empty tracks: no live data, a poll not open yet, or counts
// hidden from the room. With nobody answered every share is 0.
function percentsOf(screen: PollScreen | null, options: number): (number | null)[] {
  if (screen === null || screen.phase === 'draft' || screen.counts === null) {
    return Array.from({ length: options }, () => null);
  }
  const { counts, answered } = screen;
  return Array.from({ length: options }, (_, i) =>
    answered > 0 ? Math.round(((counts[i] ?? 0) / answered) * 100) : 0,
  );
}

// The poll slide (light, SW-6 V7): the question, one row per option and, with live data from usePollScreen, a bar and a
// percentage per option and a footer with the state and the answer count. Without live data (the deck page, RenderPage,
// preview_deck, a viewer who can't present) it draws the question, the options and empty tracks. The join pill shows
// while the deck is live. The root carries layout-poll, as every layout's does; poll-slide wraps the content without a
// box of its own (display: contents), so the frame's flex geometry still reaches the heading and .sw-body.
export function PollLayout({ slots }: LayoutProps<'poll'>) {
  const screen = usePollScreen();
  const joined = useJoin();
  useMarkPollSlide();
  const questionRef = useRef<HTMLHeadingElement>(null);
  useFitText(questionRef, QUESTION_FIT, slots.question);

  const { options } = slots;
  const hidden = screen !== null && screen.phase === 'open' && screen.counts === null;
  const percents = percentsOf(screen, options.length);
  const correct = screen !== null && screen.answerShown ? screen.correct : null;

  return (
    <div data-testid="layout-poll" className={`sw-layout ${styles.poll}`}>
      <div data-testid={POLL_SLIDE_TEST_IDS.slide} className="contents">
        {joined?.live ? (
          <p data-testid={POLL_SLIDE_TEST_IDS.join} className={styles.join}>
            {joined.join.hostLabel} · <b>{joined.join.codeLabel}</b>
          </p>
        ) : null}
        <Kicker testId={POLL_SLIDE_TEST_IDS.kind} text={kindOf(screen)} />
        <h2
          ref={questionRef}
          data-testid={POLL_SLIDE_TEST_IDS.question}
          className={styles.question}
        >
          {slots.question}
        </h2>
        <div className={`sw-body ${styles.body}`}>
          <ol
            data-testid={POLL_SLIDE_TEST_IDS.rows}
            data-density={densityOf(options.length)}
            className={styles.rows}
          >
            {options.map((option, position) => (
              <PollBar
                key={position}
                position={position}
                label={option}
                percent={percents[position]}
                hatched={hidden}
                correct={correct === position + 1}
                faded={correct !== null && correct !== position + 1}
              />
            ))}
          </ol>
        </div>
        {screen === null ? null : (
          <div className={styles.foot}>
            <span
              data-testid={POLL_SLIDE_TEST_IDS.state}
              data-phase={screen.phase}
              className={styles.state}
            >
              {PHASE_LABEL[screen.phase]}
            </span>
            {screen.phase === 'draft' ? null : (
              <span data-testid={POLL_SLIDE_TEST_IDS.answered} className={styles.answered}>
                <b>{screen.answered}</b> {screen.answered === 1 ? 'answer' : 'answers'}
              </span>
            )}
            <span className={styles.notes}>
              {hidden ? (
                <span data-testid={POLL_SLIDE_TEST_IDS.hiddenNote} className={styles.hiddenNote}>
                  <svg viewBox="0 0 10 12" aria-hidden="true">
                    <path
                      fill="currentColor"
                      d="M2 5V3.5a3 3 0 0 1 6 0V5h.5A1.5 1.5 0 0 1 10 6.5v4A1.5 1.5 0 0 1 8.5 12h-7A1.5 1.5 0 0 1 0 10.5v-4A1.5 1.5 0 0 1 1.5 5zm1.3 0h3.4V3.5a1.7 1.7 0 0 0-3.4 0z"
                    />
                  </svg>
                  Results show when the poll closes
                </span>
              ) : null}
              {screen.pick === 'upTo' && percents[0] !== null ? (
                <span data-testid={POLL_SLIDE_TEST_IDS.manyNote}>
                  Share of people who picked each. Adds up to more than 100%.
                </span>
              ) : null}
              {screen.number > 1 ? (
                <span data-testid={POLL_SLIDE_TEST_IDS.round}>Round {screen.number}</span>
              ) : null}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
