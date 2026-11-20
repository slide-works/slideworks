import type { DeckPlaySlide } from '../api/types';
import { SlideRenderer } from '../slides/SlideRenderer';
import { SlideShowingContext } from '../slides/slideShowing';
import type { SlideDeck } from '../slides/slots';

type PlayerSlideProps = {
  slide: DeckPlaySlide;
  deck: SlideDeck;
  total: number;
  active: boolean;
  step: number;
};

// One slide layer on the canvas. Every slide stays in the DOM; the ones not
// showing are hidden with visibility and opacity (never display:none), taken
// out of the accessibility tree, and inert, so nothing on them takes focus or
// clicks even where a child sets its own visibility. React 19 renders the
// boolean `inert` as the attribute. The fade is off under reduced motion.
export function PlayerSlide({ slide, deck, total, active, step }: PlayerSlideProps) {
  return (
    <div
      data-testid={`player-slide-${slide.number}`}
      data-active={active}
      aria-hidden={active ? undefined : true}
      inert={!active}
      className="absolute inset-0 motion-safe:transition-[opacity,visibility] motion-safe:duration-300"
      style={{ visibility: active ? 'visible' : 'hidden', opacity: active ? 1 : 0 }}
    >
      <SlideShowingContext value={active}>
        <SlideRenderer slide={slide} deck={deck} total={total} step={step} />
      </SlideShowingContext>
    </div>
  );
}
