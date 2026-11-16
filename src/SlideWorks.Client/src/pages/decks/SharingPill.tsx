import type { DeckRow } from '../../api/types';

// Who can open the deck: Private, Link, or how many named viewers.
export function SharingPill({ sharing, viewerCount }: Pick<DeckRow, 'sharing' | 'viewerCount'>) {
  const [label, tone] =
    sharing === 'named'
      ? [`${viewerCount} ${viewerCount === 1 ? 'viewer' : 'viewers'}`, 'bg-ok-soft text-ok']
      : sharing === 'link'
        ? ['Link', 'bg-accent-soft text-accent-dark']
        : ['Private', 'bg-panel-2 text-ink-muted'];
  return (
    <span
      data-testid="deck-sharing"
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ${tone}`}
    >
      {label}
    </span>
  );
}
