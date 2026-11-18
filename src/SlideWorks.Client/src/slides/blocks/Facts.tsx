import type { CSSProperties } from 'react';
import type { FactsBlock } from '../slots';
import { toneRule } from './tone';
import styles from './Facts.module.css';

type FactsProps = { facts: FactsBlock };

// A row of 2–4 equal fact tiles, 20 px apart: a small-capitals label over a
// bold value, under a 5 px rule in the tile's tone. The row clips its
// overflow, so a too-wide font never draws text over the blocks around it.
export function Facts({ facts }: FactsProps) {
  const columns = { '--facts-count': facts.length } as CSSProperties;
  return (
    <div data-testid="block-facts" className={styles.facts} style={columns}>
      {facts.map((fact, i) => {
        const rule =
          fact.tone === undefined
            ? undefined
            : ({ '--tile-rule': `var(--sw-${toneRule(fact.tone)})` } as CSSProperties);
        return (
          <div
            key={i}
            data-testid="facts-tile"
            data-tone={fact.tone}
            className={styles.tile}
            style={rule}
          >
            <div data-testid="facts-label" className={styles.label}>
              {fact.label}
            </div>
            <div data-testid="facts-value" className={styles.value}>
              {fact.value}
            </div>
          </div>
        );
      })}
    </div>
  );
}
