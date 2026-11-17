import type { ReactNode } from 'react';
import { useCallback, useEffect, useMemo, useRef, useSyncExternalStore } from 'react';
import type { DeckPlayView } from '../api/types';
import { useLiveDrive } from '../live/useLiveDrive';
import { useLiveSessionContext } from '../live/LiveSessionProvider';
import { deepDivesOf } from '../look/deepDivesOf';
import { nextPosition, previousPosition } from '../presentation/navigate';
import type { PresentationUpdate } from '../presentation/types';
import type { SlideDeck } from '../slides/slots';
import { Blackout } from './Blackout';
import { FIRST_POSITION, type PlayerPosition } from './parseHash';
import { PlayerControls } from './PlayerControls';
import { PlayerStage } from './PlayerStage';
import { usePlayerKeys } from './usePlayerKeys';
import { usePlayerNavigation } from './usePlayerNavigation';

function subscribeFullscreen(onChange: () => void): () => void {
  document.addEventListener('fullscreenchange', onChange);
  return () => document.removeEventListener('fullscreenchange', onChange);
}

function isFullscreen(): boolean {
  return document.fullscreenElement !== null;
}

// What audience mode gives the player (P8): the presentation it follows. position is the last state's slide and step
// (null before the first), live says whether it is being followed now, and driven that a presenter or phone is
// connected, so the player's own controls, clicks and swipes do nothing. send moves the presentation (D4).
export type AudienceControl = {
  position: PlayerPosition | null;
  live: boolean;
  driven: boolean;
  black: boolean;
  skipDeepDives: boolean;
  send(update: PresentationUpdate): void;
};

function noop() {}

// The deck player (K12): the scaled 1920×1080 stage over its control bar,
// driven by keys, clicks, swipes and the `#slide.step` hash.
export function Player({
  deck,
  audience,
  notice,
}: {
  deck: DeckPlayView;
  audience?: AudienceControl;
  // A message over the stage, inside the element that goes fullscreen.
  notice?: ReactNode;
}) {
  const root = useRef<HTMLElement>(null);
  const stepCounts = useMemo(() => deck.slides.map((slide) => slide.steps), [deck.slides]);
  // What every slide knows about the deck, with the look from the play view.
  const slideDeck = useMemo<SlideDeck>(
    () => ({
      title: deck.title,
      author: deck.author,
      parts: deck.parts,
      look: deck.look,
      deepDives: deepDivesOf(deck.slides),
    }),
    [deck.title, deck.author, deck.parts, deck.look, deck.slides],
  );
  // The owner's player while live (SW-5 D18): one more presenter-role client of the presentation, so it follows the
  // state like the audience window and publishes only the user's own moves.
  const session = useLiveSessionContext();
  const live = session.live;
  const ownPosition = useRef<PlayerPosition>(FIRST_POSITION);
  const liveDrive = useLiveDrive(deck.id, live && !audience, ownPosition, session.reportError);
  const control = audience ?? liveDrive;
  const navigation = usePlayerNavigation(
    stepCounts,
    control?.position ? { position: control.position, live: control.live } : null,
  );
  const { position } = navigation;
  useEffect(() => {
    ownPosition.current = position;
  }, [position]);
  const deepDives = useMemo(
    () => new Set(slideDeck.deepDives.map((dive) => dive.number)),
    [slideDeck.deepDives],
  );
  // While live, every move is the presentation's: the keys and the control bar ask for it and render the answer.
  const moves = useMemo(() => {
    if (!control?.live) return navigation;
    const { black, skipDeepDives, send } = control;
    const go = (to: PlayerPosition) => {
      // At the end of the deck, or with only skipped deep dives ahead, there is nothing to send.
      if (to.slide === position.slide && to.step === position.step) return;
      send({ ...to, black, skipDeepDives });
    };
    return {
      next: () => go(nextPosition(position, stepCounts, deepDives, skipDeepDives)),
      previous: () => go(previousPosition(position, stepCounts, deepDives, skipDeepDives)),
      first: () => go({ slide: 1, step: 0 }),
      last: () => go({ slide: Math.max(stepCounts.length, 1), step: 0 }),
    };
  }, [control, navigation, position, stepCounts, deepDives]);
  const { next, previous, first, last } = moves;
  const driven = audience?.driven ?? false;

  const fullscreen = useSyncExternalStore(subscribeFullscreen, isFullscreen);
  const toggleFullscreen = useCallback(() => {
    // The browser may refuse (an iframe without allowfullscreen, a permissions
    // policy); the player stays as it is, with no unhandled rejection. Without
    // the API (iPhone Safari) the F key does nothing, as there's no button.
    if (!document.fullscreenEnabled) return;
    if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
    else root.current?.requestFullscreen().catch(() => {});
  }, []);

  const keyActions = useMemo(
    () => ({ next, previous, first, last, fullscreen: toggleFullscreen }),
    [next, previous, first, last, toggleFullscreen],
  );
  usePlayerKeys(keyActions);

  return (
    <section
      ref={root}
      data-testid="player"
      aria-label={deck.title}
      className="flex h-dvh w-full flex-col overflow-hidden bg-[#05070d]"
    >
      {audience ? (
        <div className="relative flex min-h-0 flex-1 flex-col">
          <PlayerStage
            deck={deck}
            slideDeck={slideDeck}
            position={position}
            onNext={driven ? noop : next}
            onPrevious={driven ? noop : previous}
          />
          {audience.black && <Blackout />}
        </div>
      ) : (
        <PlayerStage
          deck={deck}
          slideDeck={slideDeck}
          position={position}
          onNext={next}
          onPrevious={previous}
        />
      )}
      {!driven && (
        <PlayerControls
          deckTitle={deck.title}
          position={position}
          total={deck.slides.length}
          steps={stepCounts[position.slide - 1] ?? 0}
          canFullscreen={document.fullscreenEnabled}
          fullscreen={fullscreen}
          onPrevious={previous}
          onNext={next}
          onFullscreen={toggleFullscreen}
          joinBar={deck.look.joinBar}
          liveChip={!audience}
        />
      )}
      {notice}
    </section>
  );
}
