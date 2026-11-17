import { useId } from 'react';
import {
  pollOptionBarTestId,
  pollOptionCorrectTestId,
  pollOptionPercentTestId,
  pollOptionTestId,
  pollOptionYouTestId,
} from '../testIds';
import styles from './PollOptions.module.css';

export type PollRadioProps = {
  // The card's question: the radios' group name, and part of each radio's accessible name.
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
  // Greyed out, for a poll that isn't open yet.
  dimmed: boolean;
  onToggle: (position: number) => void;
};

// One pick-one option: a label at least 44 px tall around a real radio, with its bar and percentage behind and beside
// the text, and the You and Correct marks under it. The radio's name is the option and the question; its marks and
// percentage describe it.
export function PollRadio({
  questionId,
  position,
  text,
  checked,
  you,
  correct,
  share,
  disabled,
  dimmed,
  onToggle,
}: PollRadioProps) {
  const id = useId();
  const described = [
    you && `${id}-you`,
    correct && `${id}-correct`,
    share !== null && `${id}-share`,
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
        type="radio"
        name={questionId}
        data-testid={pollOptionTestId(position)}
        className={styles.radio}
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
