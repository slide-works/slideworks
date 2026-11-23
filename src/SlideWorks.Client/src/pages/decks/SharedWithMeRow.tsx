import type { SharedDeckRow } from '../../api/types';
import { accessLabel } from '../deck/accessLabel';
import { DeckTitleCell } from './DeckTitleCell';
import { LastChangeCell } from './LastChangeCell';
import { PresentLink } from './PresentLink';

// A deck someone shared with the signed-in person. The title opens the deck page when they can edit it, the
// player when they can only view it.
export function SharedWithMeRow({ row }: { row: SharedDeckRow }) {
  return (
    <tr
      data-testid="shared-row"
      data-deck-id={row.id}
      className="border-b border-edge last:border-b-0 hover:bg-ground/50"
    >
      <DeckTitleCell
        title={row.title}
        subtitle={row.subtitle}
        to={row.access === 'write' ? `/decks/${row.id}` : row.playUrl}
      />
      <td data-testid="shared-owner" className="px-3 text-ink">
        {row.ownerName}
      </td>
      <td data-testid="shared-access" className="px-3 whitespace-nowrap text-ink">
        {accessLabel(row.access)}
      </td>
      <td className="px-3 text-right font-mono text-[12.5px] text-ink-muted">{row.slideCount}</td>
      <LastChangeCell change={row.lastChange} ownDeck={false} />
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
