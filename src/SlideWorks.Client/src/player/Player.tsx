import { useCallback, useMemo, useRef } from 'react';
import type { DeckPlayView } from '../api/types';
import { PlayerControls } from './PlayerControls';
import { PlayerStage } from './PlayerStage';
import { usePlayerKeys } from './usePlayerKeys';
import { usePlayerNavigation } from './usePlayerNavigation';

// The deck player (K12): the scaled 1920×1080 stage over its control bar,
// driven by keys, clicks, swipes and the `#slide.step` hash.
export function Player({ deck }: { deck: DeckPlayView }) {
  const root = useRef<HTMLElement>(null);
  const stepCounts = useMemo(() => deck.slides.map((slide) => slide.steps), [deck.slides]);
  const navigation = usePlayerNavigation(stepCounts);
  const { position, next, previous, first, last } = navigation;

  const toggleFullscreen = useCallback(() => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void root.current?.requestFullscreen();
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
      <PlayerStage deck={deck} position={position} onNext={next} onPrevious={previous} />
      <PlayerControls
        position={position}
        total={deck.slides.length}
        steps={stepCounts[position.slide - 1] ?? 0}
        canFullscreen={document.fullscreenEnabled}
        onPrevious={previous}
        onNext={next}
        onFullscreen={toggleFullscreen}
      />
    </section>
  );
}
