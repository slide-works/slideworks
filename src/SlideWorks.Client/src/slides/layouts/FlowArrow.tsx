import { Reveal } from '../Reveal';
import styles from './FlowLayout.module.css';

type FlowArrowProps = {
  // The step the node it points to comes at; 0 shows it from the start.
  at: number;
  step: number;
};

// The 56 × 28 arrow between two nodes, in the 72 px column. It arrives with the
// node it points to and keeps its space while hidden.
export function FlowArrow({ at, step }: FlowArrowProps) {
  return (
    <Reveal at={at} step={step} className={styles.arrowSlot}>
      <svg
        data-testid="flow-arrow"
        className={styles.arrow}
        width="56"
        height="28"
        viewBox="0 0 56 28"
        aria-hidden="true"
      >
        <path d="M2 14H52M40 4L52 14L40 24" />
      </svg>
    </Reveal>
  );
}
