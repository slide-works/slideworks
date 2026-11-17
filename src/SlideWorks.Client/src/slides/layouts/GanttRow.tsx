import type { GanttRow as GanttRowSlot, GanttStatus } from '../slots';
import styles from './GanttRow.module.css';

// What a bar says when its row has no note.
const STATUS_LABEL: Record<GanttStatus, string> = {
  done: 'Done',
  slips: 'Slips',
  stopped: 'Stopped',
  ship: 'Ship',
};

type GanttRowProps = { row: GanttRowSlot; line: number };

// One gantt row on grid line `line`: its label in the first column, then its
// bar from its start to its end column (1-based, end inclusive) in its
// status's tone, or a ship marker. A label too long for two lines ends in an
// ellipsis and keeps its full text in a tooltip.
export function GanttRow({ row, line }: GanttRowProps) {
  const { label, start, end, status, note } = row;
  const text = note ?? STATUS_LABEL[status];
  const place = { gridRow: line, gridColumn: `${start + 1} / ${end + 2}` };
  return (
    <>
      <span
        data-testid="gantt-label"
        className={styles.label}
        style={{ gridRow: line }}
        title={label}
      >
        {label}
      </span>
      {status === 'ship' ? (
        <span data-testid="gantt-bar" data-status={status} className={styles.ship} style={place}>
          <span className={styles.note}>▲ {text}</span>
        </span>
      ) : (
        <span data-testid="gantt-bar" data-status={status} className={styles.bar} style={place}>
          <span className={styles.note}>{text}</span>
        </span>
      )}
    </>
  );
}
