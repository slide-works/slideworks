import type { DeckPlayView } from '../api/types';
import { SlideRenderer } from '../slides/SlideRenderer';

// TODO: stub that stacks every slide with slide 1 visible; the real player
// (scaling, steps, #slide.step links, keys, click and swipe) replaces it.
export function Player({ deck }: { deck: DeckPlayView }) {
  return (
    <div data-testid="player">
      {deck.slides.map((slide, index) => (
        <div key={slide.number} style={{ visibility: index === 0 ? 'visible' : 'hidden' }}>
          <SlideRenderer slide={slide} deck={deck} total={deck.slides.length} step={0} />
        </div>
      ))}
    </div>
  );
}
