import { Badge } from '../blocks/Badge';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps, TableCell } from '../slots';
import styles from './TableLayout.module.css';

// A headline over a table of 2–6 columns and 1–6 rows. A toned cell is drawn
// as a badge in its tone, and highlightRow (1-based) gets the accent band.
export function TableLayout({ slots }: LayoutProps<'table'>) {
  const { kicker, headline, columns, rows, highlightRow } = slots;
  return (
    <div data-testid="layout-table" className="sw-layout">
      <SlideHeading layout="table" kicker={kicker} headline={headline} />
      <div data-testid="table-body" className="sw-body">
        <table className={styles.table}>
          <thead>
            <tr>
              {columns.map((column, i) => (
                <th key={i} data-testid="table-column" scope="col" className={styles.column}>
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, r) => (
              <tr
                key={r}
                data-testid="table-row"
                data-highlight={r + 1 === highlightRow ? 'true' : undefined}
                className={styles.row}
              >
                {row.map((cell, c) => (
                  <td key={c} data-testid="table-cell" className={styles.cell}>
                    <Cell cell={cell} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Cell({ cell }: { cell: TableCell }) {
  return typeof cell === 'string' ? cell : <Badge text={cell.text} tone={cell.tone} />;
}
