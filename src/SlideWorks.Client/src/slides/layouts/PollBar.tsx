import {
  pollSlideBarTestId,
  pollSlideCorrectTestId,
  pollSlidePercentTestId,
  pollSlideRowTestId,
} from '../../polls/testIds';
import styles from './PollLayout.module.css';

export type PollBarProps = {
  // The option's 0-based position.
  position: number;
  label: string;
  // The share of people who answered that picked this option, as a whole percentage, or null to draw an empty track
  // with no percentage (no live data, a poll not open yet, counts hidden from the room).
  percent: number | null;
  // Counts are hidden from the room: the track is hatched.
  hatched: boolean;
  // The answer is shown and this is it: the bar takes the positive colour and the label a tick.
  correct: boolean;
  // The answer is shown and this isn't it.
  faded: boolean;
};

// One option of the poll slide: its label, its track with a bar sized by the share, and the percentage.
export function PollBar({ position, label, percent, hatched, correct, faded }: PollBarProps) {
  const rowClass = [styles.row, correct ? styles.correct : '', faded ? styles.faded : '']
    .filter(Boolean)
    .join(' ');
  return (
    <li data-testid={pollSlideRowTestId(position)} className={rowClass}>
      <span className={styles.label}>
        {correct ? (
          <span
            data-testid={pollSlideCorrectTestId(position)}
            role="img"
            aria-label="Correct answer"
            className={styles.tick}
          >
            <svg viewBox="0 0 16 16" aria-hidden="true">
              <path
                d="M3.5 8.5l3 3 6-7"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </span>
        ) : null}
        <span className={styles.labelText}>{label}</span>
      </span>
      <span
        className={hatched ? `${styles.track} ${styles.hatched}` : styles.track}
        aria-hidden="true"
      >
        {percent === null ? null : (
          <i
            data-testid={pollSlideBarTestId(position)}
            className={styles.bar}
            style={{ width: `${Math.min(percent, 100)}%` }}
          />
        )}
      </span>
      <span data-testid={pollSlidePercentTestId(position)} className={styles.percent}>
        {percent === null ? null : `${percent}%`}
      </span>
    </li>
  );
}
