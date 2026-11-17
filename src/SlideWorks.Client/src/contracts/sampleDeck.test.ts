// The cross-language check of the sample deck (K3): src/contracts/sample-deck.json
// is the server's SlideValidator fixture and the client's play view, so it must
// fit the client's types too.
//
// At compile time (tsc in `pnpm run build`) the JSON import satisfies the play
// view's JSON form. A JSON import types its strings as `string`, so `layout`
// can't satisfy the LayoutName union and a slide's slots can't be tied to its
// layout (see JsonView in test-support/fixtures/widen.ts); the run-time checks
// below cover exactly those: every layout is known, every slot key belongs to
// its layout, and every required slot key is there.
import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import type { DeckPlayView } from '../api/types';
import { LAYOUT_NAMES, SLOT_KEYS, type LayoutName, type Slots } from '../slides/slots';
import type { JsonView } from '../test-support/fixtures/widen';

const sampleDeck = sampleDeckJson satisfies JsonView<DeckPlayView>;

// The slot keys a layout can't do without, derived from the optional markers in
// Slots: the `satisfies` clause rejects a key that is optional or unknown, and
// RequiredKeysComplete rejects a table that leaves a required key out.
type RequiredKeys<T> = {
  [K in keyof T]-?: Record<never, never> extends Pick<T, K> ? never : K;
}[keyof T];

const REQUIRED_SLOT_KEYS = {
  title: ['title'],
  divider: ['title'],
  bullets: ['headline', 'items'],
  'two-column': ['headline', 'left', 'right'],
  practice: ['headline', 'do', 'comes_back', 'avoid'],
  compare: ['headline', 'failure', 'success'],
  'big-number': ['headline', 'value', 'caption'],
  quote: ['quote', 'attribution'],
  image: ['headline', 'image'],
  table: ['headline', 'columns', 'rows'],
  chart: ['headline', 'kind', 'xLabels', 'series'],
  gantt: ['headline', 'columns', 'rows'],
  'numbered-cards': ['headline', 'cards'],
  rules: ['headline', 'rules'],
  cards: ['headline', 'cards'],
  close: ['headline'],
  join: [],
} as const satisfies { [L in LayoutName]: readonly RequiredKeys<Slots[L]>[] };

type UnlistedRequiredKey = {
  [L in LayoutName]: Exclude<RequiredKeys<Slots[L]>, (typeof REQUIRED_SLOT_KEYS)[L][number]>;
}[LayoutName];
type AssertTrue<T extends true> = T;
type RequiredKeysComplete = AssertTrue<[UnlistedRequiredKey] extends [never] ? true : false>;

const isLayout = (layout: string): layout is LayoutName =>
  (LAYOUT_NAMES as readonly string[]).includes(layout);

describe('sample-deck.json against the client slot types', () => {
  it('has slides', () => {
    expect(sampleDeck.slides.length).toBeGreaterThan(0);
  });

  it('uses only layouts in SLOT_KEYS', () => {
    for (const slide of sampleDeck.slides) {
      expect(Object.keys(SLOT_KEYS), `slide ${slide.number}`).toContain(slide.layout);
    }
  });

  it('uses only slot keys of each slide layout', () => {
    for (const slide of sampleDeck.slides) {
      if (!isLayout(slide.layout)) throw new Error(`slide ${slide.number}: unknown layout`);
      const allowed: readonly string[] = SLOT_KEYS[slide.layout];
      for (const key of Object.keys(slide.slots))
        expect(allowed, `slide ${slide.number} (${slide.layout}).${key}`).toContain(key);
    }
  });

  it('sets every required slot key of each slide layout', () => {
    for (const slide of sampleDeck.slides) {
      if (!isLayout(slide.layout)) throw new Error(`slide ${slide.number}: unknown layout`);
      for (const key of REQUIRED_SLOT_KEYS[slide.layout])
        expect(Object.keys(slide.slots), `slide ${slide.number} (${slide.layout})`).toContain(key);
    }
  });

  it('lists only real slot keys as required', () => {
    const complete: RequiredKeysComplete = true;
    expect(complete).toBe(true);
    for (const layout of LAYOUT_NAMES) {
      const allowed: readonly string[] = SLOT_KEYS[layout];
      for (const key of REQUIRED_SLOT_KEYS[layout]) expect(allowed, layout).toContain(key);
    }
  });
});
