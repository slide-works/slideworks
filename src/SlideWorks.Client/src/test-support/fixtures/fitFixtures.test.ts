// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { CARD_FIT_FIXTURES } from '../../slides/layouts/CardLayouts.fixtures';
import { FIT_FIXTURES } from '../../slides/layouts/ContentLayouts.fixtures';
import { DATA_FIT_FIXTURES } from '../../slides/layouts/DataLayouts.fixtures';
import { LEGACY_FIT_FIXTURES, fitFixture, fitFixturesOf, legacyFitFixturesOf } from './fitFixtures';

describe('fitFixtures', () => {
  it('a known name gives its fixture', () => {
    expect(fitFixture('bulletsEight').layout).toBe('bullets');
    expect(fitFixture('qaTitle').layout).toBe('qa');
  });

  it('an unknown name throws', () => {
    expect(() => fitFixture('noSuchFixture')).toThrow('fit-fixtures has no fixture noSuchFixture');
    expect(() => fitFixture('toString')).toThrow('fit-fixtures has no fixture toString');
  });

  it('a layout gives its own fixtures, and an unknown layout none', () => {
    expect(Object.keys(fitFixturesOf('rules')).sort()).toEqual([
      'rulesProse',
      'rulesTitleCase',
      'rulesWide',
    ]);
    expect(fitFixturesOf('no-such-layout')).toEqual({});
  });

  it('every old name is still in the folder', () => {
    expect(LEGACY_FIT_FIXTURES).toHaveLength(26);
    expect(new Set(LEGACY_FIT_FIXTURES).size).toBe(26);
    for (const name of LEGACY_FIT_FIXTURES) expect(fitFixture(name).grow).toBeInstanceOf(Array);
  });

  it('the family lists hold exactly the old names', () => {
    const names = [
      ...Object.keys(CARD_FIT_FIXTURES),
      ...Object.keys(FIT_FIXTURES),
      ...Object.keys(DATA_FIT_FIXTURES),
      'joinKicker',
      'pollEight',
      'qaTitle',
    ];

    expect(names.sort()).toEqual([...LEGACY_FIT_FIXTURES].sort());
  });

  it('a family list leaves out a layout that is not its own', () => {
    expect(Object.keys(legacyFitFixturesOf(['join', 'poll'])).sort()).toEqual([
      'joinKicker',
      'pollEight',
    ]);
  });
});
