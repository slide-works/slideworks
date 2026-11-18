import type { CSSProperties } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import styles from './GanttLayout.module.css';
import { GanttRow } from './GanttRow';

// A headline over a timeline: a label column, then one grid column per
// slot column, with a GanttRow per row and the caption under it. A callout of
// its own sits under all of it, from the start or as the slide's only step;
// the rows shrink to make room.
export function GanttLayout({ slots, step }: LayoutProps<'gantt'>) {
  const { kicker, headline, columns, rows, caption, callout } = slots;
  const grid = {
    '--sw-gantt-columns': columns.length,
    '--sw-gantt-rows': rows.length,
  } as CSSProperties;
  const figure = (
    <figure className={callout === undefined ? styles.figure : `${styles.figure} ${styles.fit}`}>
      <div className={styles.grid} style={grid}>
        {columns.map((column, i) => (
          <span
            key={i}
            data-testid="gantt-column"
            className={styles.column}
            style={{ gridColumn: i + 2 }}
          >
            {column}
          </span>
        ))}
        {rows.map((row, i) => (
          <GanttRow key={i} row={row} line={i + 2} />
        ))}
      </div>
      {caption !== undefined ? (
        <figcaption data-testid="gantt-caption" className={styles.caption}>
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
  return (
    <div data-testid="layout-gantt" className="sw-layout">
      <SlideHeading
        layout="gantt"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      {callout === undefined ? (
        <div data-testid="gantt-body" className="sw-body">
          {figure}
        </div>
      ) : (
        <div data-testid="gantt-body" className={`sw-body ${styles.stack}`}>
          <div className={styles.fill}>{figure}</div>
          <BodyCallout callout={callout} step={step} at={calloutStep(0, callout)} />
        </div>
      )}
    </div>
  );
}
