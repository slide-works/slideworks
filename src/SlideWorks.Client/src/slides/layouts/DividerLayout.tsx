import { useRef } from 'react';
import { Journey } from '../blocks/Journey';
import { useSlidePart } from '../slidePart';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './DividerLayout.module.css';

// The title is set at 104 px in at most two lines. Capitals or wide letters can
// wrap a title the validator accepts onto more, so it then shrinks until it fits
// two, never below 64 px (a 44-character title of W fits two lines there).
const TITLE_FIT: FitBudget = { start: 104, floor: 64, lines: 2, lineHeight: 1.02 };

// A section divider (dark) that opens a part: the part's number, the title, an
// optional sub, and the journey through the deck's parts. The number is the
// slide's part, so a divider outside any part shows none.
export function DividerLayout({ slots, deck }: LayoutProps<'divider'>) {
  const part = useSlidePart();
  const titleRef = useRef<HTMLHeadingElement>(null);
  useFitText(titleRef, TITLE_FIT, slots.title);
  return (
    <div data-testid="layout-divider" className={`sw-layout ${styles.divider}`}>
      {part !== null ? (
        <p data-testid="divider-number" className={styles.number}>
          {String(part).padStart(2, '0')}
        </p>
      ) : null}
      <h2 ref={titleRef} data-testid="divider-title" className={styles.heading}>
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
