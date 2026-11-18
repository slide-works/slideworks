import { LedgerHead } from '../../pages/decks/LedgerHead';
import type { TemplateRow } from '../types';
import { TemplateRowView, type RowKind } from './TemplateRowView';

const COLUMNS = [
  { label: 'Template' },
  { label: 'Type' },
  { label: 'Palette' },
  { label: 'Sharing' },
  { label: 'Decks', align: 'right' as const },
  { label: 'Status' },
  { label: 'Actions', hidden: true },
];

export type TemplateGroup = { kind: RowKind; label: string; rows: TemplateRow[] };

// The groups under one table head. A group with no rows is left out.
export function TemplateTable({
  groups,
  onArchive,
  onStartFrom,
}: {
  groups: TemplateGroup[];
  onArchive(row: TemplateRow): void;
  onStartFrom(row: TemplateRow): void;
}) {
  return (
    <div className="panel overflow-x-auto">
      <table aria-label="Templates" className="w-full border-collapse text-meta">
        <LedgerHead columns={COLUMNS} />
        {groups
          .filter((group) => group.rows.length > 0)
          .map((group) => (
            <tbody key={group.kind} aria-label={group.label}>
              <tr>
                <th
                  scope="rowgroup"
                  colSpan={COLUMNS.length}
                  className="border-b border-edge bg-panel-2/60 px-3 py-1.5 text-left text-[11px] font-semibold tracking-wider text-ink-muted uppercase"
                >
                  {group.label}
                </th>
              </tr>
              {group.rows.map((row) => (
                <TemplateRowView
                  key={row.id}
                  row={row}
                  kind={group.kind}
                  onArchive={onArchive}
                  onStartFrom={onStartFrom}
                />
              ))}
            </tbody>
          ))}
      </table>
    </div>
  );
}
