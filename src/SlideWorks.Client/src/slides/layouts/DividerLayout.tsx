import { useId, useLayoutEffect, useRef, useState } from 'react';
import { partsOf } from '../../api/parts';
import { Journey } from '../blocks/Journey';
import { useSlidePart } from '../slidePart';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './DividerLayout.module.css';

// The title is set at 104 px in at most two lines. Capitals or wide letters can
// wrap a title the validator accepts onto more, so it then shrinks until it fits
// two, never below 64 px (a 44-character title of W fits two lines there).
const TITLE_FIT: FitBudget = { start: 104, floor: 64, lines: 2, lineHeight: 1.02 };

// The stack (number to deep-dive list) may be 846 px tall: from the content top
// at y 150 to y 996, above the footer.
const STACK_HEIGHT = 846;

// At most six deep-dive pills, then a '+N more' pill. Each pill is one line,
// so with a long title, a sub and the journey fewer may fit: the list then
// drops pills, one at a time, until the stack ends at y 996.
const MOST_DEEP_DIVES = 6;

// The height of the layout's stack, from the top of its first child to the
// bottom of its last, in canvas px.
function stackHeight(root: HTMLElement): number {
  const stack = [...root.children].filter(
    (child): child is HTMLElement =>
      child instanceof HTMLElement && getComputedStyle(child).position !== 'absolute',
  );
  const first = stack[0];
  const last = stack[stack.length - 1];
  if (first === undefined || last === undefined) return 0;
  return last.offsetTop + last.offsetHeight - first.offsetTop;
}

// A section divider (dark) that opens a part: the part's number, the title, an
// optional sub, the journey through the deck's parts and, when the template's
// chrome lists them (deepDivesOnDivider), the headlines of the part's deep
// dives. The number is the slide's part, so a divider outside any part shows
// none, and lists none; in an unnumbered part the mark is the number.
export function DividerLayout({ slots, deck }: LayoutProps<'divider'>) {
  const part = useSlidePart();
  // An unnumbered part shows its mark in place of the number, with no journey
  // and no deep-dive list.
  const mark = part === null ? null : (partsOf(deck.parts)[part - 1]?.mark ?? null);
  const deepDives =
    deck.look.template.chrome.deepDivesOnDivider && part !== null && mark === null
      ? deck.deepDives.filter((deepDive) => deepDive.part === part)
      : [];
  const titleRef = useRef<HTMLHeadingElement>(null);
  useFitText(titleRef, TITLE_FIT, slots.title);

  // How many pills are shown: up to MOST_DEEP_DIVES, fewer while the stack
  // runs past y 996. Anything that changes the stack starts again from the most.
  const rootRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const labelId = useId();
  const stack = [
    slots.title,
    slots.sub ?? '',
    partsOf(deck.parts)
      .map((part) => part.name)
      .join('\n'),
    deepDives.map((deepDive) => deepDive.headline).join('\n'),
  ].join('\n\n');
  const [fit, setFit] = useState({ stack, shown: MOST_DEEP_DIVES });
  const shown = Math.min(deepDives.length, fit.stack === stack ? fit.shown : MOST_DEEP_DIVES);
  const more = deepDives.length - shown;
  useLayoutEffect(() => {
    const root = rootRef.current;
    const list = listRef.current;
    if (root === null || list === null) return;
    // A ResizeObserver reports once on observe, again when the title's fit or
    // the fonts change a size, and when a hidden thumbnail is shown.
    const observer = new ResizeObserver(() => {
      if (root.offsetHeight === 0) return;
      if (shown > 0 && stackHeight(root) > STACK_HEIGHT) setFit({ stack, shown: shown - 1 });
    });
    observer.observe(root);
    observer.observe(list);
    if (titleRef.current !== null) observer.observe(titleRef.current);
    return () => observer.disconnect();
  }, [stack, shown]);
  return (
    <div ref={rootRef} data-testid="layout-divider" className={`sw-layout ${styles.divider}`}>
      {slots.image !== undefined ? (
        <img
          data-testid="divider-image"
          className={styles.photo}
          src={`/api/images/${encodeURIComponent(slots.image.image)}`}
          alt={slots.image.alt}
        />
      ) : null}
      {part !== null ? (
        <p data-testid="divider-number" className={styles.number}>
          {mark ?? String(part).padStart(2, '0')}
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
      {deck.parts.length > 0 && mark === null ? <Journey parts={deck.parts} part={part} /> : null}
      {deepDives.length > 0 ? (
        <div ref={listRef} data-testid="divider-deep-dives" className={styles.deepDives}>
          <p id={labelId} className={styles.deepDivesLabel}>
            Deep dives
          </p>
          <ul aria-labelledby={labelId} className={styles.deepDiveList}>
            {deepDives.slice(0, shown).map((deepDive) => (
              <li key={deepDive.number} data-testid="divider-deep-dive" title={deepDive.headline}>
                {deepDive.headline}
              </li>
            ))}
            {more > 0 ? <li data-testid="divider-deep-dives-more">+{more} more</li> : null}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
