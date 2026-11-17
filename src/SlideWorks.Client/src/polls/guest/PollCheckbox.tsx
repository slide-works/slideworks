import { useId } from 'react';
import {
  pollOptionBarTestId,
  pollOptionCorrectTestId,
  pollOptionPercentTestId,
  pollOptionTestId,
  pollOptionYouTestId,
} from '../testIds';
import styles from './PollOptionsMany.module.css';

export type PollCheckboxProps = {
  // The card's question: part of each checkbox's accessible name.
  questionId: string;
  // The option's 0-based position, the same index as picks.
  position: number;
  text: string;
  // Selected on the phone.
  checked: boolean;
  // The server holds this option as the guest's answer: the "You" mark.
  you: boolean;
  // The presenter has shown this option as the answer: the "Correct" mark.
  correct: boolean;
  // The option's share of the answers, a whole percentage, or null while the phone may not show counts.
  share: number | null;
  disabled: boolean;
  // Greyed out, for a poll that isn't open yet and for an unpicked option at the limit.
  dimmed: boolean;
  // The id of the text that says why this option can't be ticked, when it is disabled at the limit.
  limitNoteId: string | null;
  onToggle: (position: number) => void;
};

// One pick-several option: a label at least 44 px tall around a real checkbox, with its bar and percentage and the You
// and Correct marks. The checkbox's name is the option and the question; its marks, percentage and, at the limit, the
// reason it can't be ticked describe it.
export function PollCheckbox({
  questionId,
  position,
  text,
  checked,
  you,
  correct,
  share,
  disabled,
  dimmed,
  limitNoteId,
  onToggle,
}: PollCheckboxProps) {
  const id = useId();
  const described = [
    you && `${id}-you`,
    correct && `${id}-correct`,
    share !== null && `${id}-share`,
    limitNoteId,
  ].filter(Boolean);

  return (
    <label
      className={[styles.option, correct && styles.correct, dimmed && styles.dimmed]
        .filter(Boolean)
        .join(' ')}
    >
      {share !== null && (
        <span
          data-testid={pollOptionBarTestId(position)}
          aria-hidden
          className={styles.bar}
          style={{ width: `${share}%` }}
        />
      )}
      <input
        type="checkbox"
        data-testid={pollOptionTestId(position)}
        className={styles.box}
        checked={checked}
        disabled={disabled}
        aria-labelledby={`${id}-text ${questionId}`}
        aria-describedby={described.length > 0 ? described.join(' ') : undefined}
        onChange={() => onToggle(position)}
      />
      <span className={styles.text}>
        <span id={`${id}-text`}>{text}</span>
        {(you || correct) && (
          <span className={styles.marks}>
            {you && (
              <em
                id={`${id}-you`}
                data-testid={pollOptionYouTestId(position)}
                className={`${styles.mark} ${styles.you}`}
              >
                You
              </em>
            )}
            {correct && (
              <em
                id={`${id}-correct`}
                data-testid={pollOptionCorrectTestId(position)}
                className={`${styles.mark} ${styles.correctMark}`}
              >
                Correct
              </em>
            )}
          </span>
        )}
      </span>
      {share !== null && (
        <span
          id={`${id}-share`}
          data-testid={pollOptionPercentTestId(position)}
          className={styles.percent}
        >
          {`${share}%`}
        </span>
      )}
    </label>
  );
}
