import type { DeckPageSlide, SlideFinding } from '../../api/types';
import { SlideRenderer } from '../../slides/SlideRenderer';
import type { SlideDeck } from '../../slides/slots';
import { SlideChecksBadge } from './SlideChecksBadge';
import { useFitWidth } from './useFitWidth';

const SLIDE_WIDTH = 1920;

type SlideThumbProps = {
  slide: DeckPageSlide;
  deck: SlideDeck;
  total: number;
  findings?: SlideFinding[];
  selected: boolean;
  onSelect(): void;
};

// One slide of the grid: the real slide, every step revealed, scaled down with
// a CSS transform, under a button that selects it.
export function SlideThumb({
  slide,
  deck,
  total,
  findings = [],
  selected,
  onSelect,
}: SlideThumbProps) {
  const { ref, scale } = useFitWidth<HTMLDivElement>(SLIDE_WIDTH);
  return (
    <li data-testid="deck-thumb" data-slide={slide.number} className="grid gap-1">
      <div
        ref={ref}
        className={`relative aspect-video overflow-hidden rounded-md border bg-panel ${
          selected ? 'border-accent ring-2 ring-accent' : 'border-edge'
        }`}
      >
        {/* A picture of the slide: hidden from assistive tech, and inert so nothing in it takes focus. */}
        <div
          aria-hidden
          inert
          className="pointer-events-none absolute top-0 left-0 origin-top-left"
          style={{ transform: `scale(${scale})`, visibility: scale > 0 ? 'visible' : 'hidden' }}
        >
          <SlideRenderer slide={slide} deck={deck} total={total} step={slide.steps} />
        </div>
        <div className="pointer-events-none absolute top-1 right-1">
          <SlideChecksBadge slide={slide.number} findings={findings} />
        </div>
        <button
          type="button"
          data-testid="deck-thumb-select"
          aria-pressed={selected}
          aria-label={`Slide ${slide.number}, ${slide.layout}`}
          className="absolute inset-0 cursor-pointer rounded-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          onClick={onSelect}
        />
      </div>
      <div className="flex justify-between text-xs text-ink-muted">
        <span>{slide.number}</span>
        <span className="font-mono">{slide.layout}</span>
      </div>
    </li>
  );
}
