import { Link } from 'react-router-dom';
import type { SharingLevel } from '../../api/types';
import { bundleUrl } from '../bundle';
import { TEMPLATES_TEST_IDS } from '../testIds';
import type { TemplateRow } from '../types';

export type RowKind = 'mine' | 'shared' | 'builtIn';

const SHARING: Record<SharingLevel, string> = {
  private: 'Private',
  named: 'Named people',
  link: 'Link',
};

function statusOf(row: TemplateRow, kind: RowKind): { label: string; tone: string } {
  if (kind === 'builtIn') return { label: 'Built-in', tone: 'bg-panel-2 text-ink-muted' };
  if (row.status === 'archived') return { label: 'Archived', tone: 'bg-panel-2 text-ink-muted' };
  if (row.status === 'draft') return { label: 'Draft', tone: 'bg-warn-soft text-warn' };
  return { label: `Published · v${row.version}`, tone: 'bg-ok-soft text-ok' };
}

const CELL = 'px-3 py-2 align-middle';
const ACTION = 'text-meta font-medium text-accent-dark hover:underline';

// One template of the list. Every string the row shows is the template's own and is drawn as text.
export function TemplateRowView({
  row,
  kind,
  onArchive,
  onStartFrom,
}: {
  row: TemplateRow;
  kind: RowKind;
  onArchive(row: TemplateRow): void;
  onStartFrom(row: TemplateRow): void;
}) {
  const status = statusOf(row, kind);
  return (
    <tr
      data-testid={TEMPLATES_TEST_IDS.row}
      data-template-id={row.id}
      className="border-b border-edge last:border-b-0 hover:bg-ground/50"
    >
      <td className={CELL}>
        <span data-testid={TEMPLATES_TEST_IDS.rowName} className="block font-semibold text-ink">
          {row.name}
        </span>
        <span
          data-testid={TEMPLATES_TEST_IDS.rowShortName}
          className="block font-mono text-[12px] text-ink-muted"
        >
          {row.shortName}
        </span>
        {kind === 'shared' && row.owner !== null && (
          <span className="block text-[12px] text-ink-muted">by {row.owner}</span>
        )}
      </td>
      <td className={`${CELL} text-ink-muted`}>{row.type}</td>
      <td className={`${CELL} text-ink-muted`}>
        {row.palette}
        {row.paletteLocked && ' · locked'}
      </td>
      <td className={CELL}>
        <span className="inline-flex rounded-full bg-panel-2 px-2 py-0.5 text-xs font-medium whitespace-nowrap text-ink-muted">
          {SHARING[row.sharing]}
        </span>
      </td>
      <td className={`${CELL} text-right font-mono text-[12.5px] text-ink-muted`}>
        {row.deckCount}
      </td>
      <td className={CELL}>
        <span
          data-testid={TEMPLATES_TEST_IDS.rowStatus}
          data-status={row.status}
          data-version={row.version}
          className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ${status.tone}`}
        >
          {status.label}
        </span>
      </td>
      <td className={`${CELL} text-right whitespace-nowrap`}>
        <span className="inline-flex items-center gap-3">
          {kind === 'mine' && (
            <>
              <Link
                to={`/templates/${encodeURIComponent(row.id)}/edit`}
                aria-label={`Edit ${row.name}`}
                className={ACTION}
              >
                Edit
              </Link>
              <a
                href={bundleUrl(row.id)}
                download
                aria-label={`Download ${row.name} as a bundle`}
                className={ACTION}
              >
                Download
              </a>
              {row.status !== 'archived' && (
                <button
                  type="button"
                  aria-haspopup="dialog"
                  aria-label={`Archive ${row.name}`}
                  className={ACTION}
                  onClick={() => onArchive(row)}
                >
                  Archive
                </button>
              )}
            </>
          )}
          {kind === 'shared' && (
            <Link
              to={`/t/${encodeURIComponent(row.id)}`}
              aria-label={`View ${row.name}`}
              className={ACTION}
            >
              View
            </Link>
          )}
          {kind === 'builtIn' && (
            <button
              type="button"
              aria-haspopup="dialog"
              aria-label={`Start from ${row.name}`}
              className={ACTION}
              onClick={() => onStartFrom(row)}
            >
              Start from this
            </button>
          )}
        </span>
      </td>
    </tr>
  );
}
