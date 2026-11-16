import type { DeckRow } from '../../api/types';
import { DeckTitleCell } from './DeckTitleCell';
import { LastChangeCell } from './LastChangeCell';
import { PresentLink } from './PresentLink';
import { SharingPill } from './SharingPill';

// One of the presenter's decks. The title opens the deck page.
export function OwnedDeckRow({ row }: { row: DeckRow }) {
  return (
    <tr
      data-testid="deck-row"
      data-deck-id={row.id}
      className="border-b border-edge last:border-b-0 hover:bg-ground/50"
    >
      <DeckTitleCell title={row.title} subtitle={row.subtitle} to={`/decks/${row.id}`} />
      <td className="px-3 text-right font-mono text-[12.5px] text-ink-muted">{row.slideCount}</td>
      <LastChangeCell change={row.lastChange} ownDeck />
      <td className="px-3">
        <SharingPill sharing={row.sharing} viewerCount={row.viewerCount} />
      </td>
      <td
        data-testid="deck-updated"
        className="px-3 text-right font-mono text-[12.5px] whitespace-nowrap text-ink-muted"
      >
        {row.updatedLabel}
      </td>
      <td className="px-3 py-1.5 text-right">
        <PresentLink playUrl={row.playUrl} />
      </td>
    </tr>
  );
}
