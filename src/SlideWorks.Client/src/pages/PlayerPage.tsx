import { useParams } from 'react-router-dom';

// /d/:id/:slug (and /d/:id): the player, open to anonymous viewers
// (GET /api/play/{id}, DeckPlayView).
// TODO: shell; the player page wiring (load the play view, render Player, and
// redirect /d/:id to add the slug) replaces it.
export function PlayerPage() {
  const { id } = useParams();
  return (
    <main data-testid="player-page" data-deck-id={id} className="p-6 text-meta text-ink-muted">
      Loading the deck…
    </main>
  );
}
