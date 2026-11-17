import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import type { AudienceControl } from '../player/Player';
import type { PlayerPosition } from '../player/parseHash';
import type { PresentationUpdate } from '../presentation/types';
import { clientId, usePresentation } from '../presentation/usePresentation';

const PUBLISH_FAILED = 'Guests may not be following: could not publish your position.';
const REFUSED = 'Guests cannot follow: the presentation refused this player.';

// While the owner is live in the plain player (SW-5 D18), the player is one more presenter-role client of the
// deck's presentation, under the same rules as SW-4's presenter window and the phone: it renders the presentation's
// state and keeps no position of its own (D4), and it publishes only a move the user makes in it. It never overwrites
// what is already running:
// - a presentation another presenter window or phone is driving: the player adopts its position, and moves its view to it
// - a presentation nobody drives (the one Go live just started, or one left idle): the player's own position is where
//   the talk starts, published once; the black screen and skip setting stay as the state has them
// - after a reconnect the player adopts the state it finds and publishes nothing
// While not active it joins nothing and returns undefined, so the player keeps its own position as before.
// seed holds the player's own position (it changes with every render, so a ref), read once for the first publish.
export function useLiveDrive(
  deckId: string,
  active: boolean,
  seed: RefObject<PlayerPosition>,
  onError: (message: string) => void,
): AudienceControl | undefined {
  const presentation = usePresentation(active ? deckId : '', 'presenter', 'Player');
  const { status, state, update } = presentation;
  // True once the player has decided whether to adopt the state or seed it; false until then and after End.
  const [decided, setDecided] = useState(false);
  const deciding = useRef(false);
  // Ending the live session starts the next one afresh (adjusted while rendering, so a stale decision never paints).
  if (!active && decided) setDecided(false);

  useEffect(() => {
    if (status === 'refused') onError(REFUSED);
  }, [status, onError]);

  // Another presenter window or phone is driving the presentation: adopt its position, once (adjusted while rendering).
  const driven =
    state !== null &&
    state.connected.some((c) => c.role !== 'audience' && c.clientId !== clientId());
  if (active && !decided && status === 'live' && driven) setDecided(true);

  useEffect(() => {
    if (!active) {
      deciding.current = false;
      return;
    }
    if (decided || deciding.current || status !== 'live' || state === null || driven) return;
    // Nobody drives it: the player's own position is where the talk starts.
    deciding.current = true;
    const { slide, step } = seed.current;
    update({ slide, step, black: state.black, skipDeepDives: state.skipDeepDives }).then(
      () => setDecided(true),
      () => {
        // Say so, and carry on following: the player's next move publishes again.
        onError(PUBLISH_FAILED);
        setDecided(true);
      },
    );
  }, [active, decided, driven, status, state, update, seed, onError]);

  const send = useCallback(
    (to: PresentationUpdate) => {
      update(to).catch(() => onError(PUBLISH_FAILED));
    },
    [update, onError],
  );

  const slide = state?.slide;
  const step = state?.step;
  const black = state?.black ?? false;
  const skipDeepDives = state?.skipDeepDives ?? false;
  return useMemo(
    () =>
      active && status !== 'refused'
        ? {
            position: decided && slide !== undefined && step !== undefined ? { slide, step } : null,
            live: decided && status === 'live',
            driven: false,
            black,
            skipDeepDives,
            send,
          }
        : undefined,
    [active, status, decided, slide, step, black, skipDeepDives, send],
  );
}
