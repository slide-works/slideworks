import { useLayoutEffect, useRef } from 'react';
import { partsOf, type PartWire } from '../../api/parts';
import styles from './Journey.module.css';

export type JourneyState = 'past' | 'on' | 'next';

function stateOf(index: number, part: number | null): JourneyState {
  if (part === null) return 'next';
  if (index + 1 < part) return 'past';
  return index + 1 === part ? 'on' : 'next';
}

// The names share one size, from 18 px down to 13 px in whole pixels, and their
// tracking narrows from 0.14 em to 0.08 em over the same steps (D24).
const NAME_SIZE_MAX = 18;
const NAME_SIZE_MIN = 13;
const TRACKING_WIDE = 0.14;
const TRACKING_TIGHT = 0.08;

// Steps the shared name size down until no name has a word wider than its
// stop, or the size reaches 13 px. A word wider than its stop is broken by
// overflow-wrap, which hides the overflow, so the check lets words overflow.
function fitNames(list: HTMLElement) {
  if (list.offsetWidth === 0) return;
  const names = [...list.querySelectorAll<HTMLElement>('[data-journey-name]')];
  for (const name of names) name.style.overflowWrap = 'normal';
  let size = NAME_SIZE_MAX;
  for (;;) {
    const tracking =
      TRACKING_WIDE -
      ((TRACKING_WIDE - TRACKING_TIGHT) * (NAME_SIZE_MAX - size)) / (NAME_SIZE_MAX - NAME_SIZE_MIN);
    list.style.setProperty('--journey-name-size', `${size}px`);
    list.style.setProperty('--journey-name-tracking', `${tracking}em`);
    if (size <= NAME_SIZE_MIN || !names.some((name) => name.scrollWidth > name.clientWidth)) break;
    size -= 1;
  }
  for (const name of names) name.style.overflowWrap = '';
}

// The deck's numbered parts as a timeline, with `part` (1-based, into the whole
// list) as the current one: past parts filled, the current one glowing and
// future ones hollow. Each stop shows the part's name and its caption, or
// 'Part n'. Rendered from the deck, never set by an agent.
export function Journey({ parts, part }: { parts: PartWire[]; part: number | null }) {
  const stops = partsOf(parts)
    .map((deckPart, index) => ({ ...deckPart, index }))
    .filter((stop) => stop.mark === null);
  const listRef = useRef<HTMLOListElement>(null);
  const names = stops.map((stop) => stop.name).join('\n');
  useLayoutEffect(() => {
    const list = listRef.current;
    if (list === null) return;
    const fit = () => fitNames(list);
    fit();
    // Again when the list is resized, and when fonts arrive and change a width.
    const observer = new ResizeObserver(fit);
    observer.observe(list);
    void document.fonts.ready.then(fit);
    return () => observer.disconnect();
  }, [names]);
  return (
    <ol
      ref={listRef}
      data-testid="block-journey"
      className={styles.journey}
      style={{ gridTemplateColumns: `repeat(${stops.length}, minmax(0, 1fr))` }}
    >
      {stops.map(({ name, caption, index }) => {
        const state = stateOf(index, part);
        return (
          <li
            // Part names may repeat, so the index keys the item.
            key={index}
            data-testid="journey-part"
            data-state={state}
            aria-current={state === 'on' ? 'step' : undefined}
            className={styles.part}
          >
            <b data-journey-name="" className={styles.name} title={name}>
              {name}
            </b>
            <span className={styles.number}>{caption ?? `Part ${index + 1}`}</span>
          </li>
        );
      })}
    </ol>
  );
}
