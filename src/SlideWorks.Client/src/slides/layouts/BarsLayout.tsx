import type { CSSProperties } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { barHeight, planBars, rowGap } from '../diagrams/barsGeometry';
import { describeBars } from '../diagrams/describeBars';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { BarRow } from './BarRow';
import styles from './BarsLayout.module.css';

// 2–6 horizontal bars that compare named things by length, on a track 1248 px
// wide (barsGeometry has the numbers). A bar may carry a note, a dashed outline
// for what it was (named by a legend), a tone and a tag. With reveal
// 'one-by-one' bar n arrives at step n, and a callout that reveals 'last' is
// the step after them. Bars do not grow. The figure clips its overflow.
export function BarsLayout({ slots, step }: LayoutProps<'bars'>) {
  const { kicker, headline, bars, unit, values, wasLabel, caption, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  const shown = values !== 'hidden';
  const plan = planBars(bars, unit, shown);
  const rows = { gap: `${rowGap(bars.length)}px` } as CSSProperties;
  return (
    <div data-testid="layout-bars" className="sw-layout">
      <SlideHeading
        layout="bars"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="bars-body" className={`sw-body ${styles.body}`}>
        <figure
          data-testid="bars-figure"
          aria-label={describeBars({ bars, unit, values, wasLabel })}
          className={styles.figure}
        >
          {wasLabel !== undefined ? (
            <div data-testid="bars-legend" className={styles.legend}>
              <span className={styles.swatch} />
              {wasLabel}
            </div>
          ) : null}
          <div className={styles.rows} style={rows}>
            {bars.map((bar, i) => (
              <BarRow
                key={i}
                bar={bar}
                placement={plan.placements[i]}
                unit={unit}
                shown={shown}
                height={barHeight(bars.length)}
                index={i}
                step={oneByOne ? step : undefined}
              />
            ))}
          </div>
          {caption !== undefined ? (
            <figcaption data-testid="bars-caption" className={styles.caption}>
              {caption}
            </figcaption>
          ) : null}
        </figure>
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? bars.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}
