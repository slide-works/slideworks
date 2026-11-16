import { useParams } from 'react-router-dom';

// /decks/:id: the deck page (GET /api/decks/{id}, DeckPageView).
// TODO: shell; the deck page replaces it.
export function DeckPage() {
  const { id } = useParams();
  return <section data-testid="deck-page" data-deck-id={id} />;
}
