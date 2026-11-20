import type { ComponentType } from 'react';
import { lookStyle } from '../look/lookStyle';
import { LAYOUTS } from './layouts';
import { SlideFrame } from './SlideFrame';
import { SlideNumberContext } from './slideNumber';
import { SlidePartContext } from './slidePart';
import {
  LAYOUT_SURFACE,
  type LayoutName,
  type LayoutProps,
  type SlideDeck,
  type Slots,
  type Surface,
} from './slots';

// What a slide needs to render; both the play view's and the deck page's slides fit.
export type RenderableSlide = {
  [L in LayoutName]: {
    number: number;
    layout: L;
    part: number | null;
    deepDive: boolean;
    // The slide's own surface on a layout that can be either; left out or null, the layout's.
    surface?: Surface | null;
    slots: Slots[L];
  };
}[LayoutName];

type SlideRendererProps = {
  slide: RenderableSlide;
  deck: SlideDeck;
  total: number;
  step: number;
};

// One slide at its fixed 1920×1080 size, through the layout registry inside
// the theme frame. The player and the deck page's thumbnails scale it to fit.
// The deck's look goes on the slide's root as --sw-* custom properties, so
// changing the look re-skins every slide, dark ones included (SW-2 L10).
export function SlideRenderer({ slide, deck, total, step }: SlideRendererProps) {
  // The registry is keyed by layout, so this pairs the slide's layout with its
  // own slots; TypeScript can't correlate the two through the union by itself.
  const Layout = LAYOUTS[slide.layout] as ComponentType<LayoutProps<LayoutName>>;
  const own = LAYOUT_SURFACE[slide.layout];
  const surface = slide.surface ?? (own === 'either' ? 'light' : own);
  return (
    <div
      data-testid={`slide-${slide.number}`}
      data-layout={slide.layout}
      className="relative overflow-hidden"
      style={{
        ...lookStyle(deck.look),
        width: 'var(--sw-slide-width)',
        height: 'var(--sw-slide-height)',
      }}
    >
      <SlideFrame
        layout={slide.layout}
        surface={surface}
        part={slide.part}
        parts={deck.parts}
        number={slide.number}
        total={total}
        deckTitle={deck.title}
        author={deck.author}
        look={deck.look}
        deepDive={slide.deepDive}
        titleStamp={slide.layout === 'title' ? (slide.slots.stamp ?? null) : null}
      >
        <SlidePartContext value={slide.part}>
          <SlideNumberContext value={slide.number}>
            <Layout slots={slide.slots} step={step} deck={deck} />
          </SlideNumberContext>
        </SlidePartContext>
      </SlideFrame>
    </div>
  );
}
