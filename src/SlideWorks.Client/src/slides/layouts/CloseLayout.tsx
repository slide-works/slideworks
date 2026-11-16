import { useRef } from 'react';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './CloseLayout.module.css';

// The headline is set at 112 px in at most four shown lines, the validator's
// limit. Its estimate assumes ordinary letters, so capitals or wide letters can
// wrap an accepted headline onto more; it then shrinks until it fits four,
// never below 60 px: a line of 22 capital Ws fits the 1400 px box at 63 px.
const HEADLINE_FIT: FitBudget = { start: 112, floor: 60, lines: 4, lineHeight: 1.02 };

// The closing slide (dark): a short headline whose \n breaks start new lines,
// an optional sub in a highlighted box, and an optional author.
export function CloseLayout({ slots }: LayoutProps<'close'>) {
  const lines = slots.headline.split('\n');
  const headlineRef = useRef<HTMLHeadingElement>(null);
  useFitText(headlineRef, HEADLINE_FIT, slots.headline);
  return (
    <div data-testid="layout-close" className={`sw-layout ${styles.close}`}>
      <h2 ref={headlineRef} data-testid="close-headline" className={styles.heading}>
        {lines.map((line, index) => (
          <span key={index} data-testid="close-line" className={styles.line}>
            {line}
          </span>
        ))}
      </h2>
      {slots.sub !== undefined ? (
        <p data-testid="close-sub" className={styles.sub}>
          {slots.sub}
        </p>
      ) : null}
      {slots.author !== undefined ? (
        <p data-testid="close-author" className={styles.byline}>
          {slots.author}
        </p>
      ) : null}
    </div>
  );
}
