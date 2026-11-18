// The cross-language check of the contrast pairs: src/contracts/contrast-pairs.json
// is written from the server's PaletteContrast.Pairs, and the ContrastPair union
// names exactly its pairs.
import { describe, expect, it } from 'vitest';
import contrastPairs from '@contracts/contrast-pairs.json';
import type { ContrastPair } from './types';

// One entry of the file. A plain pair has no over and no share; a mixed-ground
// pair has both: its ground is back mixed at share over the token over.
type ContrastPairEntry = {
  name: string;
  fore: string;
  back: string;
  needed: number;
  over?: string;
  share?: number;
};

// A missing or extra member of the union fails the type check of this object.
const UNION: Record<ContrastPair, true> = {
  body: true,
  secondary: true,
  'card-bold': true,
  'headline-accent': true,
  'on-accent': true,
  'on-accent-2': true,
  'deep-dive-pill': true,
  'panel-accent': true,
  'panel-positive': true,
  'panel-negative': true,
  'panel-inverse': true,
  'badge-positive': true,
  'badge-negative': true,
  'dark-subtitle': true,
  'soft-accent': true,
  'soft-inverse': true,
  'deep-dive-list': true,
  'dark-kicker': true,
  'dark-chrome': true,
  'dark-soft-accent': true,
  'dark-soft-positive': true,
  'dark-soft-negative': true,
  'pin-solid-accent': true,
};

const MEMBERS = ['name', 'fore', 'back', 'needed', 'over', 'share'];

describe('contrast-pairs.json', () => {
  const entries: ContrastPairEntry[] = contrastPairs;

  it('names exactly the members of the ContrastPair union', () => {
    expect(entries.map((entry) => entry.name).sort()).toEqual(Object.keys(UNION).sort());
  });

  it('holds only the members of a pair, with over and share together or not at all', () => {
    for (const entry of entries) {
      expect(MEMBERS).toEqual(expect.arrayContaining(Object.keys(entry)));
      expect(typeof entry.fore).toBe('string');
      expect(typeof entry.back).toBe('string');
      expect(entry.needed).toBeGreaterThan(1);
      expect(entry.over === undefined).toBe(entry.share === undefined);
    }
  });
});
