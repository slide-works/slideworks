import fixtures from '@contracts/card-scale-fixtures.json';
import { describe, expect, it, vi } from 'vitest';
import { cardListAttrs, isLarge, type ScaleCard } from './cardScale';

const cases: { cards: ScaleCard[]; large: boolean }[] = fixtures;

const card = (title: string, text = 'Text', chips?: string[]): ScaleCard => ({
  title,
  text,
  chips,
});

describe('isLarge', () => {
  it('has every shared case, some of them with an empty chips list', () => {
    expect(cases.length).toBeGreaterThanOrEqual(12);
    expect(
      cases.filter((c) => c.cards.some((x) => x.chips?.length === 0)).length,
    ).toBeGreaterThanOrEqual(4);
  });

  it.each(cases.map((c, i) => [i, c] as const))(
    'matches the shared cases: case %i',
    (_, { cards, large }) => {
      expect(isLarge(cards)).toBe(large);
    },
  );

  it('counts a letter with its combining marks as one character', () => {
    expect(isLarge([card('é'.repeat(32))])).toBe(true);
    expect(isLarge([card('é'.repeat(33))])).toBe(false);
    expect(isLarge([card('Title', '\u{1F600}'.repeat(70))])).toBe(true);
    expect(isLarge([card('Title', '\u{1F600}'.repeat(71))])).toBe(false);
  });
});

describe('isLarge without Intl.Segmenter', () => {
  // A fresh copy of the module, loaded while Intl.Segmenter is what the test put there.
  async function loadWith(replacement: unknown): Promise<typeof import('./cardScale')> {
    const original = Object.getOwnPropertyDescriptor(Intl, 'Segmenter');
    try {
      if (replacement === undefined) {
        delete (Intl as { Segmenter?: unknown }).Segmenter;
      } else {
        Object.defineProperty(Intl, 'Segmenter', { ...original, value: replacement });
      }
      vi.resetModules();
      const fresh = await import('./cardScale');
      // The first count is where the segmenter is made, so it runs in here.
      expect(fresh.isLarge([card('Title')])).toBe(true);
      return fresh;
    } finally {
      if (original !== undefined) Object.defineProperty(Intl, 'Segmenter', original);
      vi.resetModules();
    }
  }

  it('still imports and counts code points when the browser has none', async () => {
    const fresh = await loadWith(undefined);

    expect(Intl.Segmenter).toBeTypeOf('function');
    expect(fresh.isLarge([card('a'.repeat(32), 'b'.repeat(70))])).toBe(true);
    expect(fresh.isLarge([card('a'.repeat(33))])).toBe(false);
    expect(fresh.isLarge([card('Title', 'b'.repeat(71))])).toBe(false);
    // A code point outside the BMP is one character, not two UTF-16 units.
    expect(fresh.isLarge([card('Title', '\u{1F600}'.repeat(70))])).toBe(true);
    expect(fresh.isLarge([card('Title', '\u{1F600}'.repeat(71))])).toBe(false);
    expect(fresh.cardListAttrs([card('One'), card('Two')])).toEqual({
      'data-count': 2,
      'data-scale': 'large',
    });
  });

  it('counts code points when making the segmenter throws', async () => {
    const fresh = await loadWith(function Segmenter() {
      throw new RangeError('no segmenter here');
    });

    expect(fresh.isLarge([card('a'.repeat(32))])).toBe(true);
    expect(fresh.isLarge([card('a'.repeat(33))])).toBe(false);
  });
});

describe('cardListAttrs', () => {
  it('gives the count and the scale', () => {
    expect(cardListAttrs([card('One'), card('Two')])).toEqual({
      'data-count': 2,
      'data-scale': 'large',
    });
    expect(cardListAttrs([card('One'), card('Two', 'Text', ['Chip']), card('Three')])).toEqual({
      'data-count': 3,
      'data-scale': 'regular',
    });
  });

  it.each(cases.map((c, i) => [i, c] as const))(
    'matches the shared cases: case %i',
    (_, { cards, large }) => {
      expect(cardListAttrs(cards)).toEqual({
        'data-count': cards.length,
        'data-scale': large ? 'large' : 'regular',
      });
    },
  );
});
