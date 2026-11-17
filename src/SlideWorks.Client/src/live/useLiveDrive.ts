import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import type { AudienceControl } from '../player/Player';
import type { PlayerPosition } from '../player/parseHash';
import type { PresentationState, PresentationUpdate } from '../presentation/types';
import { clientId, usePresentation } from '../presentation/usePresentation';

const PUBLISH_FAILED = 'Guests may not be following: could not publish your position.';
const SLOW_JOIN_MS = 1000;
const REFUSED = 'Guests cannot follow: the presentation refused this player.';

// Nobody else drives it and nothing has moved since it started (the timer starts on the first move): the one case
// where the player's own position may become the talk's. Anything else is somebody's talk, even with nobody connected
// right now (a window reloading, a phone asleep, a talk left idle), so the player adopts it.
function isUntouchedAndAlone(state: PresentationState): boolean {
  const others = state.connected.some((c) => c.role !== 'audience' && c.clientId !== clientId());
  return (
    !others &&
    state.timer.state === 'notStarted' &&
    state.slide === 1 &&
    state.step === 0 &&
    !state.black
  );
}

// While the owner is live in the plain player (SW-5 D18), the player is one more presenter-role client of the deck's
// presentation, under the same rules as SW-4's presenter window and the phone: it renders the presentation's state
// and publishes only a move the user makes in it. It never overwrites what is already running:
// - a presentation that has been moved, or that another presenter window or phone is connected to: the player adopts
//   its position and moves its view to it
// - a presentation nobody is connected to and nothing has moved (the one Go live just started): the player's own
//   position is where the talk starts, published once; the black screen and skip setting stay as the state has them
// - a join that is not answered within a second freezes the moves and shows a Connecting notice: until then a press
//   moves the view as before, and the seed reads it
// - after a reconnect the player adopts the state it finds and publishes nothing, and its moves are frozen meanwhile
// While not active it joins nothing and returns undefined, so the player keeps its own position as before.
// seed holds the player's own position (it changes with every render, so a ref), read once for the first publish.
//
// Moves are never computed from the last state alone: every publish records its target, and latest() answers with it
// until the hub has answered them all, so presses inside one round trip send distinct targets and a state echoed
// late never jumps the view back. The target goes when the last publish settles, and the server's state is the position.
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

  // The newest position asked for and how many publishes the hub hasn't answered yet. The ref is for the key handlers,
  // which run between renders; the state is what the view shows.
  const targetRef = useRef<PlayerPosition | null>(null);
  const inFlightRef = useRef(0);
  const [target, setTarget] = useState<PlayerPosition | null>(null);
  const [inFlight, setInFlight] = useState(0);
  if (inFlight === 0 && target !== null) setTarget(null);

  // A first join that takes longer than a second freezes the moves and a notice says the player is connecting. A press
  // inside the first second moves the view and the seed reads it, so Go live doesn't flash a notice or drop keys.
  const waiting = active && !decided && status !== 'refused';
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    if (!waiting) return;
    const timer = setTimeout(() => setSlow(true), SLOW_JOIN_MS);
    return () => {
      clearTimeout(timer);
      setSlow(false);
    };
  }, [waiting]);

  useEffect(() => {
    if (status === 'refused') onError(REFUSED);
  }, [status, onError]);

  const slide = state?.slide;
  const step = state?.step;
  useEffect(() => {
    // The state is the position again once every publish has settled, and a connection that is down has none.
    if (inFlight === 0 || status !== 'live') targetRef.current = null;
  }, [inFlight, status, slide, step]);

  const publish = useCallback(
    (to: PresentationUpdate): Promise<void> => {
      targetRef.current = { slide: to.slide, step: to.step };
      setTarget(targetRef.current);
      inFlightRef.current += 1;
      setInFlight(inFlightRef.current);
      return update(to)
        .catch(() => onError(PUBLISH_FAILED))
        .finally(() => {
          inFlightRef.current -= 1;
          setInFlight(inFlightRef.current);
        });
    },
    [update, onError],
  );

  // Adopt the state: somebody else's talk (adjusted while rendering).
  if (active && !decided && status === 'live' && state !== null && !isUntouchedAndAlone(state)) {
    setDecided(true);
  }

  useEffect(() => {
    if (!active) {
      deciding.current = false;
      return;
    }
    if (decided || deciding.current || status !== 'live' || state === null) return;
    // Untouched and alone: the player's own position is where the talk starts.
    deciding.current = true;
    const { slide: ownSlide, step: ownStep } = seed.current;
    setDecided(true);
    if (ownSlide === state.slide && ownStep === state.step) return;
    void publish({
      slide: ownSlide,
      step: ownStep,
      black: state.black,
      skipDeepDives: state.skipDeepDives,
    });
  }, [active, decided, status, state, seed, publish]);

  const send = useCallback(
    (to: PresentationUpdate) => {
      void publish(to);
    },
    [publish],
  );
  const latest = useCallback(() => targetRef.current, []);

  const black = state?.black ?? false;
  const skipDeepDives = state?.skipDeepDives ?? false;
  const live = status === 'live';
  return useMemo(
    () =>
      active && status !== 'refused'
        ? {
            position:
              decided && slide !== undefined && step !== undefined
                ? (live && target) || { slide, step }
                : null,
            live: decided && live,
            frozen: (waiting && slow) || (decided && !live),
            reconnecting: decided && !live,
            connecting: waiting && slow,
            driven: false,
            black,
            skipDeepDives,
            send,
            latest,
          }
        : undefined,
    [
      active,
      status,
      decided,
      live,
      waiting,
      slow,
      target,
      slide,
      step,
      black,
      skipDeepDives,
      send,
      latest,
    ],
  );
}
