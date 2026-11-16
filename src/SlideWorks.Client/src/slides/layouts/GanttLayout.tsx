import type { CSSProperties } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import type { GanttRow, GanttStatus, LayoutProps } from '../slots';
import styles from './GanttLayout.module.css';

// What a bar says when its row has no note.
const STATUS_LABEL: Record<GanttStatus, string> = {
  done: 'Done',
  slips: 'Slips',
  stopped: 'Stopped',
  ship: 'Ship',
};

// A headline over a timeline: a label column, then one grid column per
// slot column. Each row's bar runs from its start to its end column (1-based,
// end inclusive) in its status's tone; a ship row draws a ship marker instead.
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
              <Row key={i} row={row} line={i + 2} />
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

function Row({ row, line }: { row: GanttRow; line: number }) {
  const { label, start, end, status, note } = row;
  const text = note ?? STATUS_LABEL[status];
  const place = { gridRow: line, gridColumn: `${start + 1} / ${end + 2}` };
  return (
    <>
      <span data-testid="gantt-label" className={styles.label} style={{ gridRow: line }}>
        {label}
      </span>
      {status === 'ship' ? (
        <span data-testid="gantt-bar" data-status={status} className={styles.ship} style={place}>
          ▲ {text}
        </span>
      ) : (
        <span data-testid="gantt-bar" data-status={status} className={styles.bar} style={place}>
          <span className={styles.note}>{text}</span>
        </span>
      )}
    </>
  );
}
