import { partsOf, type PartWire } from '../../api/parts';
import styles from './Journey.module.css';

export type JourneyState = 'past' | 'on' | 'next';

function stateOf(index: number, part: number | null): JourneyState {
  if (part === null) return 'next';
  if (index + 1 < part) return 'past';
  return index + 1 === part ? 'on' : 'next';
}

// The deck's parts as a timeline, with `part` (1-based) as the current one:
// past parts filled, the current one glowing and future ones hollow.
// Rendered from the deck, never set by an agent.
export function Journey({ parts, part }: { parts: PartWire[]; part: number | null }) {
  return (
    <ol
      data-testid="block-journey"
      className={styles.journey}
      style={{ gridTemplateColumns: `repeat(${parts.length}, minmax(0, 1fr))` }}
    >
      {partsOf(parts).map(({ name }, index) => {
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
            <b className={styles.name} title={name}>
              {name}
            </b>
            <span className={styles.number}>Part {index + 1}</span>
          </li>
        );
      })}
    </ol>
  );
}
