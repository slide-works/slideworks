import type { PresenterView } from '../api/types';

type Props = {
  view: PresenterView;
  current: number;
  skipDeepDives: boolean;
  onJump(n: number): void;
  onClose(): void;
};

// The phone's slides sheet: every slide of the deck as a row, the current one marked; a tap jumps there.
export function SlidesSheet({ view, current, onJump, onClose }: Props) {
  // TODO: placeholder sheet until the slides sheet is built: thumbnails (PresenterThumb), the deep-dive marks with
  // skipDeepDives, and the sheet's open and close replace the plain rows.
  return (
    <div data-testid="slides-sheet" role="dialog" aria-label="Slides" className="grid gap-2 p-4">
      <button type="button" className="btn-ghost" onClick={onClose}>
        Close slides
      </button>
      {view.slides.map((slide) => (
        <button
          key={slide.number}
          type="button"
          data-testid={`slides-sheet-row-${slide.number}`}
          aria-current={slide.number === current ? 'true' : undefined}
          className="btn-ghost text-left"
          onClick={() => onJump(slide.number)}
        >
          {slide.headline
            ? `Jump to slide ${slide.number}: ${slide.headline}`
            : `Jump to slide ${slide.number}`}
        </button>
      ))}
    </div>
  );
}
