import type { DeckRow } from '../../api/types';
import { LedgerHead } from './LedgerHead';
import { OwnedDeckRow } from './OwnedDeckRow';

const COLUMNS = [
  { label: 'Deck' },
  { label: 'Slides', align: 'right' as const },
  { label: 'Last change' },
  { label: 'Sharing' },
  { label: 'Updated', align: 'right' as const },
  { label: '' },
];

// The presenter's own decks, as a ledger.
export function DecksTable({ rows }: { rows: DeckRow[] }) {
  return (
    <div className="panel overflow-x-auto">
      <table data-testid="decks-table" className="w-full border-collapse text-meta">
        <LedgerHead columns={COLUMNS} />
        <tbody>
          {rows.map((row) => (
            <OwnedDeckRow key={row.id} row={row} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
