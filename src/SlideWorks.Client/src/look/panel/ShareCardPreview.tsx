import type { DeckPageView } from '../../api/types';

// The deck's share card as link previews see it (GET /api/cards/{id}.png). The
// query changes with the deck's revision and sharing, so the browser fetches
// the card again whenever either changes.
export function ShareCardPreview({ deck }: { deck: DeckPageView }) {
  const src = `/api/cards/${encodeURIComponent(deck.id)}.png?v=${deck.revision}-${deck.sharing.level}`;
  return (
    <div className="grid gap-1">
      <span className="text-meta font-semibold text-ink-muted">Share card</span>
      <img
        data-testid="look-card"
        src={src}
        alt="Share card"
        width={1200}
        height={630}
        className="h-auto w-full rounded border border-edge"
      />
      <span className="text-meta text-ink-muted">
        Redrawn when the title, template or palette changes.
      </span>
    </div>
  );
}
