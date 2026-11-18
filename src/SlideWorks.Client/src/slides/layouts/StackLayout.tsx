import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { describeStack } from '../diagrams/describeStack';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import styles from './StackLayout.module.css';
import { StackSegment } from './StackSegment';

// A stack of 2–8 labelled segments that make one whole, with optional values,
// a highlighted segment and points beside it. The figure is a 500 px stack
// over the total and the caption; the points, and a callout at the foot, are
// the text column. With reveal 'one-by-one' the segments arrive from the
// bottom up (segment i of n at step n − i), and a callout that reveals 'last'
// is the step after them. With no points and no callout the figure is centred.
export function StackLayout({ slots, step }: LayoutProps<'stack'>) {
  const { kicker, headline, segments, unit, total, caption, points, side, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  const hasPoints = points !== undefined && points.length > 0;
  const alone = !hasPoints && callout === undefined;
  const valued = segments.every((s) => s.value !== undefined);
  const sum = valued ? segments.reduce((all, s) => all + (s.value ?? 0), 0) : 0;
  const figure = (
    <figure
      data-testid="stack-figure"
      aria-label={describeStack({ segments, unit, total })}
      className={styles.figure}
    >
      <div data-testid="stack-column" className={styles.stack}>
        {segments.map((segment, i) => (
          <StackSegment
            key={i}
            segment={segment}
            unit={unit}
            index={i}
            count={segments.length}
            grow={valued ? ((segment.value ?? 0) / sum) * 100 : 1}
            step={oneByOne ? step : undefined}
          />
        ))}
      </div>
      {total !== undefined ? (
        <div data-testid="stack-total" className={styles.total}>
          {total}
        </div>
      ) : null}
      {caption !== undefined ? (
        <figcaption data-testid="stack-caption" className={styles.caption}>
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
  const column = alone ? null : (
    <div data-testid="stack-text" className={styles.text}>
      {hasPoints ? (
        <ul className={styles.points}>
          {points.map((point, i) => (
            <li
              key={i}
              data-testid="stack-point"
              className={typeof point === 'string' ? styles.point : `${styles.point} ${styles.led}`}
            >
              {typeof point === 'string' ? (
                point
              ) : (
                <>
                  <b className={styles.lead}>{point.lead}</b>
                  <span>{point.text}</span>
                </>
              )}
            </li>
          ))}
        </ul>
      ) : null}
      {callout !== undefined ? (
        <div className={styles.foot}>
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? segments.length : 0, callout)}
          />
        </div>
      ) : null}
    </div>
  );
  return (
    <div data-testid="layout-stack" className="sw-layout">
      <SlideHeading
        layout="stack"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div
        data-testid="stack-body"
        data-side={side === 'left' ? 'left' : 'right'}
        data-alone={alone ? 'true' : undefined}
        className={`sw-body ${styles.body}`}
      >
        {side === 'left' ? figure : column}
        {side === 'left' ? column : figure}
      </div>
    </div>
  );
}
