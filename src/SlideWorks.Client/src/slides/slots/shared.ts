// The types every layout shares (K5): tones, fills, reveals, and what a layout
// component is given.
import type { PartWire } from '../../api/parts';
import type { LookView } from '../../api/types';
import type { LayoutName, Slots } from '../slots';

// A panel's tone (SW-2 L4). good, bad and info stay valid for ever as aliases of
// positive, negative and accent (D15); stored slides are never rewritten.
export type Tone = 'accent' | 'positive' | 'negative' | 'inverse' | 'good' | 'bad' | 'info';
// A toned panel's fill; soft when left out.
export type Fill = 'soft' | 'solid';
export type Reveal = 'all' | 'one-by-one';

// A slide flagged deep_dive, as the divider lists it: its part (1-based, or
// null), its slide number and its headline.
export type DeepDive = { part: number | null; number: number; headline: string };

// What a layout knows about its deck. The player passes the play view's values;
// the deck page's thumbnails pass the deck page's. The look preview and the
// layout review page have no deck author and pass null. look is the deck's
// resolved look, and deepDives comes from deepDivesOf(slides).
export type SlideDeck = {
  title: string;
  author: string | null;
  parts: PartWire[];
  look: LookView;
  deepDives: DeepDive[];
  // False where the slide sits under a click target (a thumbnail), so nothing
  // in it should take a click; absent means true (K20).
  interactive?: boolean;
};

// The props every layout component takes (K12). `step` is 0 before any reveal.
export type LayoutProps<L extends LayoutName> = { slots: Slots[L]; step: number; deck: SlideDeck };
