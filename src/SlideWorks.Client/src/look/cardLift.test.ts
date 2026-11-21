import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { cardLift, DARK_LIFT } from './cardLift';

const SHADOW = '0 1px 2px rgb(15 23 42 / 0.06), 0 14px 36px rgb(15 23 42 / 0.09)';

// The cases of SlideWorks.UnitTest's CardLiftTests, so the client and the server agree.
describe('cardLift', () => {
  it.each(['#0e1424', '#05070d', 'rgb(20 20 20)', '#000'])(
    'gives dark paper %s the dark lift',
    (paper) => {
      expect(cardLift(SHADOW, paper)).toBe(DARK_LIFT);
    },
  );

  it.each(['#f7f8fa', '#ffffff', '#808080', '#fff'])(
    "keeps the template's shadow on light paper %s",
    (paper) => {
      expect(cardLift(SHADOW, paper)).toBe(SHADOW);
    },
  );

  it('keeps a flat template flat on dark paper', () => {
    expect(cardLift('none', '#0e1424')).toBe('none');
  });

  it("keeps the template's shadow when the paper isn't a colour it can read", () => {
    expect(cardLift(SHADOW, 'var(--x)')).toBe(SHADOW);
    expect(cardLift(SHADOW, undefined)).toBe(SHADOW);
  });

  it('lifts only Midnight among the built-in palettes', () => {
    const lifted = palettesJson.palettes
      .filter((palette) => cardLift(SHADOW, palette.tokens.paper) === DARK_LIFT)
      .map((palette) => palette.name);
    expect(lifted).toEqual(['midnight']);
  });
});
