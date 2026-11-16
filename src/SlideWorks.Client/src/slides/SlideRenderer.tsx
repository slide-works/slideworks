import type { ComponentType } from 'react';
import { LAYOUTS } from './layouts';
import { SlideFrame } from './SlideFrame';
import { SlidePartContext } from './slidePart';
import {
  DARK_LAYOUTS,
  type LayoutName,
  type LayoutProps,
  type SlideDeck,
  type Slots,
} from './slots';

// What a slide needs to render; both the play view's and the deck page's slides fit.
export type RenderableSlide = {
  [L in LayoutName]: { number: number; layout: L; part: number | null; slots: Slots[L] };
}[LayoutName];

type SlideRendererProps = {
  slide: RenderableSlide;
  deck: SlideDeck;
  total: number;
  step: number;
};

// One slide at its fixed 1920×1080 size, through the layout registry inside
// the theme frame. The player and the deck page's thumbnails scale it to fit.
export function SlideRenderer({ slide, deck, total, step }: SlideRendererProps) {
  // The registry is keyed by layout, so this pairs the slide's layout with its
  // own slots; TypeScript can't correlate the two through the union by itself.
  const Layout = LAYOUTS[slide.layout] as ComponentType<LayoutProps<LayoutName>>;
  return (
    <div
      data-testid={`slide-${slide.number}`}
      data-layout={slide.layout}
      className="relative overflow-hidden"
      style={{ width: 'var(--sw-slide-width)', height: 'var(--sw-slide-height)' }}
    >
      <SlideFrame
        layout={slide.layout}
        dark={DARK_LAYOUTS.includes(slide.layout)}
        part={slide.part}
        parts={deck.parts}
        number={slide.number}
        total={total}
        deckTitle={deck.title}
        author={deck.author}
      >
        <SlidePartContext value={slide.part}>
          <Layout slots={slide.slots} step={step} deck={deck} />
        </SlidePartContext>
      </SlideFrame>
    </div>
  );
}
