import type { DeckPlaySlide, DeckPlayView } from '../api/types';
import { SlideRenderer } from '../slides/SlideRenderer';

type PlayerSlideProps = {
  slide: DeckPlaySlide;
  deck: DeckPlayView;
  active: boolean;
  step: number;
};

// One slide layer on the canvas. Every slide stays in the DOM; the ones not
// showing are hidden with visibility and opacity (never display:none) and
// taken out of the accessibility tree. The fade is off under reduced motion.
export function PlayerSlide({ slide, deck, active, step }: PlayerSlideProps) {
  return (
    <div
      data-testid={`player-slide-${slide.number}`}
      data-active={active}
      aria-hidden={active ? undefined : true}
      className="absolute inset-0 motion-safe:transition-[opacity,visibility] motion-safe:duration-300"
      style={{ visibility: active ? 'visible' : 'hidden', opacity: active ? 1 : 0 }}
    >
      <SlideRenderer slide={slide} deck={deck} total={deck.slides.length} step={step} />
    </div>
  );
}
