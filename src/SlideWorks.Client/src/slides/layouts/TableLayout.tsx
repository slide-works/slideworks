import type { ReactNode } from 'react';
import { Badge } from '../blocks/Badge';
import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps, TableCell } from '../slots';
import { calloutStep } from '../steps';
import styles from './TableLayout.module.css';

// A headline over a table of 2–6 columns and 1–6 rows. A toned cell is drawn
// as a badge in its tone, and highlightRow (1-based) gets the accent band.
// With reveal 'one-by-one' row n shows at step n. With 'strike-and-answer'
// every left cell shows from the start and every answer is hidden: step n
// shows row n's answer and strikes its left cell, and the rows before it dim.
// A hidden cell keeps its space, so nothing moves. A callout shows from the
// start or, with reveal 'last', as the step after the last row.
export function TableLayout({ slots, step }: LayoutProps<'table'>) {
  const { kicker, headline, columns, rows, highlightRow, reveal, callout } = slots;
  const strike = reveal === 'strike-and-answer';
  const byRow = reveal === 'one-by-one' || strike;
  return (
    <div data-testid="layout-table" className="sw-layout">
      <SlideHeading
        layout="table"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
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
                data-dim={strike && r + 1 < step ? 'true' : undefined}
                className={styles.row}
              >
                {row.map((cell, c) => {
                  const content = <Cell cell={cell} />;
                  return (
                    <td
                      key={c}
                      data-testid="table-cell"
                      data-struck={strike && c === 0 && step >= r + 1 ? 'true' : undefined}
                      className={styles.cell}
                    >
                      {withReveal(content, byRow && !(strike && c === 0), r + 1, step)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(byRow ? rows.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}

// The cell's content in a Reveal (a span, so the cell keeps its space) when its row arrives by step.
function withReveal(content: ReactNode, on: boolean, at: number, step: number) {
  return on ? (
    <Reveal as="span" at={at} step={step}>
      {content}
    </Reveal>
  ) : (
    content
  );
}

function Cell({ cell }: { cell: TableCell }) {
  return typeof cell === 'string' ? cell : <Badge text={cell.text} tone={cell.tone} />;
}
