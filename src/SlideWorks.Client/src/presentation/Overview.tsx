import { useEffect } from 'react';
import type { PresenterView } from '../api/types';

type Props = {
  view: PresenterView;
  current: number;
  skipDeepDives: boolean;
  onJump(n: number): void;
  onClose(): void;
};

// The presenter window's overview grid (O): every slide of the deck, the current one marked; a click jumps there.
// Esc closes it, and closing twice is harmless.
export function Overview({ view, current, onJump, onClose }: Props) {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  // TODO: placeholder grid until the overview is built: thumbnails (PresenterThumb), the deep-dive marks with
  // skipDeepDives, and focus handling replace the plain buttons.
  return (
    <div data-testid="overview" role="dialog" aria-label="Overview" className="grid gap-2 p-4">
      {view.slides.map((slide) => (
        <button
          key={slide.number}
          type="button"
          data-testid={`overview-slide-${slide.number}`}
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
