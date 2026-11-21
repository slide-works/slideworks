import type { CellTone } from '../slots';
import styles from './Badge.module.css';

// A short label at the top of a card, such as a role. A card's badge carries
// no tone (SW-19 D5), so it takes the accent; a table cell's badge may carry
// a good, warn, bad or info tone.
export function Badge({ text, tone }: { text: string; tone?: CellTone }) {
  return (
    <span
      data-testid="block-badge"
      data-check-size="designed"
      data-tone={tone}
      className={styles.badge}
    >
      {text}
    </span>
  );
}
