import type { SharedDeckRow } from '../../api/types';
import { LedgerHead } from './LedgerHead';
import { SharedWithMeRow } from './SharedWithMeRow';

const COLUMNS = [
  { label: 'Deck' },
  { label: 'Owner' },
  { label: 'Slides', align: 'right' as const },
  { label: 'Last change' },
  { label: 'Updated', align: 'right' as const },
  { label: 'Present', hidden: true },
];

// "Shared with you": other presenters' decks that name the signed-in person as
// a viewer. Viewers open them in the player, never on the deck page.
export function SharedDecksTable({ rows }: { rows: SharedDeckRow[] }) {
  return (
    <section data-testid="shared-with-me" className="grid gap-3">
      <h2 id="shared-with-me-heading" className="text-lg font-bold tracking-tight text-ink">
        Shared with you
      </h2>
      <div className="panel overflow-x-auto">
        <table
          aria-labelledby="shared-with-me-heading"
          className="w-full border-collapse text-meta"
        >
          <LedgerHead columns={COLUMNS} />
          <tbody>
            {rows.map((row) => (
              <SharedWithMeRow key={row.id} row={row} />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
