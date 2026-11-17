import { useParams } from 'react-router-dom';

// /decks/:id/present, the presenter window (P8): signed in, with no Shell chrome. It joins the deck's presentation as
// the presenter and shows the current slide, the next one, the notes, the timer and the overview.
export function PresenterPage() {
  const { id = '' } = useParams();
  // TODO: placeholder until the presenter window is built: the presenter view (P3), usePresentation, the panels and
  // the keys replace it.
  return (
    <main data-testid="presenter-page" data-deck-id={id} className="mx-auto max-w-md px-4 py-16">
      <h1 className="page-title">Presenter view</h1>
    </main>
  );
}
