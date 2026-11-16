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
// optional sub, the journey through the deck's parts and, when the template's
// chrome lists them (deepDivesOnDivider), the headlines of the part's deep
// dives. The number is the slide's part, so a divider outside any part shows
// none, and lists none.
export function DividerLayout({ slots, deck }: LayoutProps<'divider'>) {
  const part = useSlidePart();
  const deepDives =
    deck.look.template.chrome.deepDivesOnDivider && part !== null
      ? deck.deepDives.filter((deepDive) => deepDive.part === part)
      : [];
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
      {deepDives.length > 0 ? (
        <div data-testid="divider-deep-dives" className={styles.deepDives}>
          <p className={styles.deepDivesLabel}>Deep dives</p>
          <ul className={styles.deepDiveList}>
            {deepDives.map((deepDive) => (
              <li key={deepDive.number} data-testid="divider-deep-dive">
                {deepDive.headline}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
