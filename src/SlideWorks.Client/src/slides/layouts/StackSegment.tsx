import type { CSSProperties } from 'react';
import { toneStyle } from '../blocks/tone';
import { Reveal } from '../Reveal';
import type { StackSegment as Segment } from '../slots/stack';
import styles from './StackSegment.module.css';

const NUMBER = new Intl.NumberFormat('en', { maximumFractionDigits: 20 });

// How much of the accent's dark shade a plain segment is drawn in: 22% at the
// top to 100% at the bottom, in whole percents.
export function shadeOf(index: number, count: number): number {
  return Math.round(22 + (78 * index) / Math.max(1, count - 1));
}

type StackSegmentProps = {
  segment: Segment;
  unit?: string;
  // Place from the top (0) and the number of segments.
  index: number;
  count: number;
  // The segment's share of the stack's height, when the segments have values.
  grow: number;
  // Set with reveal 'one-by-one': the slide's step. The bottom segment comes
  // first, at step 1, and the top one at step `count`.
  step?: number;
};

// One row of the stack: a block (whose height says the value), then the label
// with its tag, and the note under it. The row keeps its space while hidden.
export function StackSegment({ segment, unit, index, count, grow, step }: StackSegmentProps) {
  const { label, value, note, tone, tag } = segment;
  const shade = shadeOf(index, count);
  const style = {
    flex: `${grow} 1 0px`,
    ...(tone === undefined
      ? {
          '--stack-block': `color-mix(in srgb, var(--sw-accent-dark) ${shade}%, var(--sw-card))`,
        }
      : toneStyle(tone, 'solid')),
  } as CSSProperties;
  const row = (
    <>
      <div
        className={styles.block}
        data-dark={tone === undefined && shade >= 55 ? 'true' : undefined}
      >
        {value !== undefined ? (
          <span data-testid="stack-value" className={styles.value}>
            {NUMBER.format(value)}
            {unit !== undefined ? <span className={styles.unit}> {unit}</span> : null}
          </span>
        ) : null}
      </div>
      <div className={styles.text}>
        <div className={styles.head}>
          <span data-testid="stack-label" className={styles.label}>
            {label}
          </span>
          {tag !== undefined ? (
            <span className={styles.tag} data-check-size="designed">
              {tag}
            </span>
          ) : null}
        </div>
        {note !== undefined ? <div className={styles.note}>{note}</div> : null}
      </div>
    </>
  );
  return (
    <div data-testid="stack-segment" data-tone={tone} className={styles.slot} style={style}>
      {step === undefined ? (
        <div className={styles.row}>{row}</div>
      ) : (
        <Reveal at={count - index} step={step} className={styles.row}>
          {row}
        </Reveal>
      )}
    </div>
  );
}
