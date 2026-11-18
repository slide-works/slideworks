import type { CSSProperties } from 'react';
import { panelTone, toneStyle } from '../blocks/tone';
import { valueText, type Placement } from '../diagrams/barsGeometry';
import { Reveal } from '../Reveal';
import type { Bar } from '../slots/bars';
import styles from './BarRow.module.css';

// The tag's text colour is the tone's: the accent, bad, good or ink.
const TAG_COLOUR = {
  accent: 'var(--sw-accent)',
  negative: 'var(--sw-bad)',
  positive: 'var(--sw-good)',
  inverse: 'var(--sw-ink)',
} as const;

type BarRowProps = {
  bar: Bar;
  placement: Placement;
  unit?: string;
  shown: boolean;
  // The bar's height, from the number of bars.
  height: number;
  // Place from the top (0).
  index: number;
  // Set with reveal 'one-by-one': the slide's step. Bar n comes at step n.
  step?: number;
};

// One row: the label (and its sub) in a 400 px column, then the track with the
// bar, its dashed outline, its note and its end label (value, unit and tag).
// The row keeps its space while hidden.
export function BarRow({ bar, placement, unit, shown, height, index, step }: BarRowProps) {
  const { label, sub, note, tag } = bar;
  const tone = panelTone(bar.tone ?? 'accent');
  const text = valueText(bar.value, unit, shown);
  const style = {
    '--bars-height': `${height}px`,
    '--bars-tag': TAG_COLOUR[tone],
    ...toneStyle(tone, 'solid'),
  } as CSSProperties;
  const row = (
    <>
      <div className={styles.labels}>
        <span data-testid="bars-label" className={styles.label}>
          {label}
        </span>
        {sub !== undefined ? <span className={styles.sub}>{sub}</span> : null}
      </div>
      <div className={styles.track}>
        <div data-testid="bars-fill" className={styles.fill} style={{ width: placement.length }}>
          {note !== undefined && placement.note === 'inside' ? (
            <span data-testid="bars-note" className={styles.inside}>
              {note}
            </span>
          ) : null}
        </div>
        {placement.outline !== null ? (
          <div data-testid="bars-was" className={styles.was} style={{ width: placement.outline }} />
        ) : null}
        <div className={styles.end} style={{ left: placement.endLeft }}>
          {note !== undefined && placement.note === 'after' ? (
            <span data-testid="bars-note" className={styles.after}>
              {note}
            </span>
          ) : null}
          {text !== '' ? (
            <span data-testid="bars-value" className={styles.value}>
              {text}
            </span>
          ) : null}
          {tag !== undefined ? (
            <span data-testid="bars-tag" className={styles.tag}>
              {tag}
            </span>
          ) : null}
        </div>
      </div>
    </>
  );
  return (
    <div data-testid="bars-bar" data-tone={tone} className={styles.slot} style={style}>
      {step === undefined ? (
        <div className={styles.row}>{row}</div>
      ) : (
        <Reveal at={index + 1} step={step} className={styles.row}>
          {row}
        </Reveal>
      )}
    </div>
  );
}
