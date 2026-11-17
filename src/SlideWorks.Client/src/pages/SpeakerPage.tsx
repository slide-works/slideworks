import { useParams } from 'react-router-dom';

// /remote/:id, the phone's speaker mode (P8): signed in, with no Shell chrome. It joins the deck's presentation as a
// phone and shows the slide first, with the notes underneath (D6), and Next and Previous buttons.
export function SpeakerPage() {
  const { id = '' } = useParams();
  // TODO: placeholder until speaker mode is built: the presenter view (P3), usePresentation, the buttons, the slides
  // sheet and the wake lock replace it.
  return (
    <main data-testid="speaker-page" data-deck-id={id} className="mx-auto max-w-md px-4 py-16">
      <h1 className="page-title">Speaker mode</h1>
    </main>
  );
}
