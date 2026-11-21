import { describe, expect, it } from 'vitest';
import { allW, capitals, proseFill, titleCase, wideCapitals, wideLetters } from './maxText';

describe('maxText', () => {
  it.each([1, 7, 40, 333])('every generator returns exactly %i characters', (length) => {
    for (const make of [proseFill, wideCapitals, allW, capitals]) {
      expect(make(length)).toHaveLength(length);
    }
    expect(wideLetters(length, 'Whom We Name ')).toHaveLength(length);
  });

  it('wideLetters throws on an empty unit instead of looping for ever', () => {
    expect(() => wideLetters(5, '')).toThrow('wideLetters: unit must not be empty');
  });

  it('allW is only W and spaces', () => {
    expect(allW(30)).toMatch(/^[W ]+$/);
  });

  it('capitals has no lower case', () => {
    expect(capitals(30)).toBe(capitals(30).toUpperCase());
  });

  it('titleCase raises the first letter of every word', () => {
    expect(titleCase('ship small, ship often')).toBe('Ship Small, Ship Often');
  });
});
