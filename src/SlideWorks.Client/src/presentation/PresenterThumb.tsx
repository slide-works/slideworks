import { useMemo, useRef } from 'react';
import type { PresenterView } from '../api/types';
import { deepDivesOf } from '../look/deepDivesOf';
import { PlayerSlide } from '../player/PlayerSlide';
import { CANVAS_HEIGHT, CANVAS_WIDTH, useCanvasScale } from '../player/useCanvasScale';
import type { SlideDeck } from '../slides/slots';

type Props = { view: PresenterView; slide: number; step?: number };

// One slide of the deck as a picture, scaled to the width it is given (16:9), in the deck's look and at the given step.
// The presenter window's now and next panes, the overview and the phone all use it. It is a picture: inert and hidden
// from assistive tech, so the button or pane around it gives it its name and nothing inside takes focus or clicks.
export function PresenterThumb({ view, slide, step = 0 }: Props) {
  const frame = useRef<HTMLDivElement>(null);
  const canvasFit = useCanvasScale(frame);
  const deck = useMemo<SlideDeck>(
    () => ({
      title: view.title,
      author: view.author,
      parts: view.parts,
      look: view.look,
      deepDives: deepDivesOf(view.slides),
    }),
    [view.title, view.author, view.parts, view.look, view.slides],
  );
  const shown = view.slides[slide - 1];

  return (
    <div
      ref={frame}
      data-testid="presenter-thumb"
      data-slide={slide}
      data-step={step}
      aria-hidden
      inert
      className="relative aspect-video w-full overflow-hidden"
    >
      {shown && (
        <div
          className="absolute top-0 left-0 overflow-hidden"
          style={{
            width: CANVAS_WIDTH,
            height: CANVAS_HEIGHT,
            transformOrigin: '0 0',
            transform: canvasFit ? `scale(${canvasFit.scale})` : undefined,
            visibility: canvasFit ? undefined : 'hidden',
          }}
        >
          <PlayerSlide slide={shown} deck={deck} total={view.slides.length} active step={step} />
        </div>
      )}
    </div>
  );
}
