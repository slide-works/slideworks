import { useRef, type MouseEvent, type TouchEvent } from 'react';
import type { DeckPlayView } from '../api/types';
import type { PlayerPosition } from './parseHash';
import { PlayerSlide } from './PlayerSlide';
import { CANVAS_HEIGHT, CANVAS_WIDTH, useCanvasScale } from './useCanvasScale';

// A horizontal swipe longer than this moves one step or slide.
const SWIPE_PX = 50;

type PlayerStageProps = {
  deck: DeckPlayView;
  position: PlayerPosition;
  onNext(): void;
  onPrevious(): void;
};

// Which step a slide shows: the current one where it is, every step on the
// slides already passed, and none on the slides still to come.
function stepFor(number: number, steps: number, position: PlayerPosition): number {
  if (number === position.slide) return position.step;
  return number < position.slide ? steps : 0;
}

// The black stage with the 1920×1080 canvas scaled to fit and centred, every
// slide stacked on it. A click on its left third goes back and on its right
// third goes forward; a swipe goes the way the finger moved.
export function PlayerStage({ deck, position, onNext, onPrevious }: PlayerStageProps) {
  const stage = useRef<HTMLDivElement>(null);
  const canvasFit = useCanvasScale(stage);
  const touchStart = useRef<{ x: number; y: number } | null>(null);

  const onClick = (event: MouseEvent<HTMLDivElement>) => {
    const box = event.currentTarget.getBoundingClientRect();
    const x = (event.clientX - box.left) / box.width;
    if (x < 1 / 3) onPrevious();
    else if (x > 2 / 3) onNext();
  };

  const onTouchStart = (event: TouchEvent<HTMLDivElement>) => {
    const touch = event.changedTouches[0];
    touchStart.current = { x: touch.clientX, y: touch.clientY };
  };

  const onTouchEnd = (event: TouchEvent<HTMLDivElement>) => {
    const start = touchStart.current;
    touchStart.current = null;
    if (!start) return;
    const touch = event.changedTouches[0];
    const dx = touch.clientX - start.x;
    const dy = touch.clientY - start.y;
    if (Math.abs(dx) <= SWIPE_PX || Math.abs(dx) <= Math.abs(dy)) return;
    if (dx < 0) onNext();
    else onPrevious();
  };

  return (
    <div
      ref={stage}
      data-testid="player-stage"
      className="relative min-h-0 flex-1 overflow-hidden"
      onClick={onClick}
      onTouchStart={onTouchStart}
      onTouchEnd={onTouchEnd}
    >
      <div
        data-testid="player-canvas"
        className="absolute top-0 left-0 overflow-hidden"
        style={{
          width: CANVAS_WIDTH,
          height: CANVAS_HEIGHT,
          transformOrigin: '0 0',
          transform: canvasFit
            ? `translate(${canvasFit.left}px, ${canvasFit.top}px) scale(${canvasFit.scale})`
            : undefined,
          visibility: canvasFit ? undefined : 'hidden',
        }}
      >
        {deck.slides.map((slide) => (
          <PlayerSlide
            key={slide.number}
            slide={slide}
            deck={deck}
            active={slide.number === position.slide}
            step={stepFor(slide.number, slide.steps, position)}
          />
        ))}
      </div>
    </div>
  );
}
