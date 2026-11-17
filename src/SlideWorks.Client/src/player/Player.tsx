import { useCallback, useMemo, useRef, useSyncExternalStore } from 'react';
import type { DeckPlayView } from '../api/types';
import { LivePublisher } from '../live/LivePublisher';
import { useLiveSessionContext } from '../live/LiveSessionProvider';
import { deepDivesOf } from '../look/deepDivesOf';
import type { SlideDeck } from '../slides/slots';
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

// The deck player (K12): the scaled 1920×1080 stage over its control bar,
// driven by keys, clicks, swipes and the `#slide.step` hash.
export function Player({ deck }: { deck: DeckPlayView }) {
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
  const navigation = usePlayerNavigation(stepCounts);
  const { position, next, previous, first, last } = navigation;

  // The owner's player publishes its position to guests while the deck is live (SW-5 D18).
  const live = useLiveSessionContext().live;

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
      <PlayerStage
        deck={deck}
        slideDeck={slideDeck}
        position={position}
        onNext={next}
        onPrevious={previous}
      />
      {live && <LivePublisher deckId={deck.id} slide={position.slide} step={position.step} />}
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
      />
    </section>
  );
}
