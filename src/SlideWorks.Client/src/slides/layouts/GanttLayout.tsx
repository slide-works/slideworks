import type { CSSProperties } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import styles from './GanttLayout.module.css';
import { GanttRow } from './GanttRow';

// A headline over a timeline: a label column, then one grid column per
// slot column, with a GanttRow per row and the caption under it.
export function GanttLayout({ slots }: LayoutProps<'gantt'>) {
  const { kicker, headline, columns, rows, caption } = slots;
  const grid = { '--sw-gantt-columns': columns.length } as CSSProperties;
  return (
    <div data-testid="layout-gantt" className="sw-layout">
      <SlideHeading layout="gantt" kicker={kicker} headline={headline} />
      <div data-testid="gantt-body" className="sw-body">
        <figure className={styles.figure}>
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
      </div>
    </div>
  );
}
