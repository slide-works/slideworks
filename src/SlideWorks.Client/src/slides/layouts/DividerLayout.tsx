import { Journey } from '../blocks/Journey';
import { useSlidePart } from '../slidePart';
import type { LayoutProps } from '../slots';
import styles from './DividerLayout.module.css';

// A section divider (dark) that opens a part: the part's number, the title, an
// optional sub, and the journey through the deck's parts. The number is the
// slide's part, so a divider outside any part shows none.
export function DividerLayout({ slots, deck }: LayoutProps<'divider'>) {
  const part = useSlidePart();
  return (
    <div data-testid="layout-divider" className={`sw-layout ${styles.divider}`}>
      {part !== null ? (
        <p data-testid="divider-number" className={styles.number}>
          {String(part).padStart(2, '0')}
        </p>
      ) : null}
      <h2 data-testid="divider-title" className={styles.heading}>
        {slots.title}
      </h2>
      {slots.sub !== undefined ? (
        <p data-testid="divider-sub" className={styles.sub}>
          {slots.sub}
        </p>
      ) : null}
      {deck.parts.length > 0 ? <Journey parts={deck.parts} part={part} /> : null}
    </div>
  );
}
