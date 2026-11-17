import { useEffect } from 'react';
import { usePresentation } from '../presentation/usePresentation';

const PUBLISH_FAILED = 'Guests may not be following: could not publish your position.';
const REFUSED = 'Guests cannot follow: the presentation refused this window.';

// While the owner is live in the plain player (SW-5 D18), publishes the player's position as the deck's presentation
// state, so guests follow it. It joins the presentation as a presenter window and sends each slide and step the player
// moves to. Drawn only while live: it renders nothing, and leaving it ends the join. A failed publish, or a join the
// server refused, goes to onError, which the live session shows.
// TODO: SW-230 decides which of this publisher and the presenter window publishes when both are open.
export function LivePublisher({
  deckId,
  slide,
  step,
  onError,
}: {
  deckId: string;
  slide: number;
  step: number;
  onError: (message: string) => void;
}) {
  const presentation = usePresentation(deckId, 'presenter', 'Player');
  const { status, update } = presentation;
  useEffect(() => {
    if (status === 'refused') onError(REFUSED);
  }, [status, onError]);
  useEffect(() => {
    if (status !== 'live') return;
    update({ slide, step, black: false, skipDeepDives: false }).catch(() =>
      onError(PUBLISH_FAILED),
    );
  }, [status, update, slide, step, onError]);
  return null;
}
