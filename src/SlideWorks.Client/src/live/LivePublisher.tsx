import { useEffect } from 'react';
import { usePresentation } from '../presentation/usePresentation';

// While the owner is live in the plain player (SW-5 D18), publishes the player's position as the deck's presentation
// state, so guests follow it. It joins the presentation as a presenter window and sends each slide and step the player
// moves to. Drawn only while live: it renders nothing, and leaving it ends the join.
export function LivePublisher({
  deckId,
  slide,
  step,
}: {
  deckId: string;
  slide: number;
  step: number;
}) {
  const presentation = usePresentation(deckId, 'presenter', 'Player');
  const { status, update } = presentation;
  useEffect(() => {
    if (status !== 'live') return;
    void update({ slide, step, black: false, skipDeepDives: false }).catch(() => undefined);
  }, [status, update, slide, step]);
  return null;
}
