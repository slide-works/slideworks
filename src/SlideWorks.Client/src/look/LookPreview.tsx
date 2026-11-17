import type { LookView } from '../api/types';
import { useFitWidth } from '../pages/deck/useFitWidth';
import { SlideRenderer, type RenderableSlide } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import { deepDivesOf } from './deepDivesOf';

const SLIDE_WIDTH = 1920;

export type LookPreviewProps = { look: LookView; slides: RenderableSlide[] };

// A few slides under a look, live and scaled down, for the New deck dialog and
// the Look panel. The slides render through SlideRenderer with every step
// revealed, so changing the look re-skins them at once. The preview has no
// deck of its own, so the frame shows no deck title, author or part tracker.
export function LookPreview({ look, slides }: LookPreviewProps) {
  const deck: SlideDeck = {
    title: '',
    author: null,
    parts: [],
    look,
    deepDives: deepDivesOf(slides),
  };
  const total = Math.max(0, ...slides.map((slide) => slide.number));
  return (
    <div
      data-testid="look-preview"
      data-template={look.template.name}
      data-palette={look.palette.name}
      className="grid gap-3"
    >
      {slides.map((slide) => (
        <PreviewSlide key={slide.number} slide={slide} deck={deck} total={total} />
      ))}
    </div>
  );
}

function PreviewSlide({
  slide,
  deck,
  total,
}: {
  slide: RenderableSlide;
  deck: SlideDeck;
  total: number;
}) {
  const { ref, scale } = useFitWidth<HTMLDivElement>(SLIDE_WIDTH);
  return (
    <div
      ref={ref}
      data-testid="look-preview-slide"
      data-slide={slide.number}
      className="relative aspect-video overflow-hidden rounded-md border border-edge bg-panel"
    >
      {/* A picture of the slide: hidden from assistive tech and inert. */}
      <div
        aria-hidden
        inert
        className="pointer-events-none absolute top-0 left-0 origin-top-left"
        style={{ transform: `scale(${scale})`, visibility: scale > 0 ? 'visible' : 'hidden' }}
      >
        <SlideRenderer slide={slide} deck={deck} total={total} step={Number.MAX_SAFE_INTEGER} />
      </div>
    </div>
  );
}
