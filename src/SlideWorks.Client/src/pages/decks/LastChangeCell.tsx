import type { LastChange } from '../../api/types';

type LastChangeCellProps = {
  change: LastChange | null;
  // On the presenter's own decks, a person's change is the presenter's own
  // (people change only their own decks, through the browser), so it reads "You".
  ownDeck: boolean;
};

// "Claude via MCP · added slide 16", "You · changed sharing", or the owner's
// name on a deck shared with you.
export function LastChangeCell({ change, ownDeck }: LastChangeCellProps) {
  return (
    <td data-testid="deck-last-change" className="px-3 text-ink-muted">
      {change === null ? (
        '—'
      ) : change.via === 'mcp' ? (
        <>
          <span className="text-ink">{change.actor}</span> via MCP · {change.summary}
        </>
      ) : (
        <>
          <span className="text-ink">{ownDeck ? 'You' : change.actor}</span> · {change.summary}
        </>
      )}
    </td>
  );
}
