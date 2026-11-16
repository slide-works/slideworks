import { describe, expect, it } from 'vitest';
import { formatHash, parseHash } from './parseHash';

// Three slides: the second has four steps.
const steps = [0, 4, 0];

describe('parseHash', () => {
  it.each([
    ['#1', { slide: 1, step: 0 }],
    ['#3', { slide: 3, step: 0 }],
    ['#2.0', { slide: 2, step: 0 }],
    ['#2.3', { slide: 2, step: 3 }],
    ['#2.4', { slide: 2, step: 4 }],
    ['2.1', { slide: 2, step: 1 }],
  ])('reads %s', (hash, expected) => {
    expect(parseHash(hash, steps)).toEqual(expected);
  });

  it('clamps a step past the slide’s last one to its last step', () => {
    expect(parseHash('#2.9', steps)).toEqual({ slide: 2, step: 4 });
    expect(parseHash('#1.2', steps)).toEqual({ slide: 1, step: 0 });
  });

  it.each([
    '',
    '#',
    '#0',
    '#4',
    '#-1',
    '#01',
    '#abc',
    '#2.',
    '#2.x',
    '#2.1.1',
    '# 2',
    '#1e2',
    '#2.-1',
  ])('refuses %j', (hash) => {
    expect(parseHash(hash, steps)).toBeNull();
  });

  it('refuses every slide of an empty deck', () => {
    expect(parseHash('#1', [])).toBeNull();
  });
});

describe('formatHash', () => {
  it('leaves the step out before the first reveal', () => {
    expect(formatHash({ slide: 3, step: 0 })).toBe('#3');
  });

  it('writes slide.step once a step shows', () => {
    expect(formatHash({ slide: 3, step: 2 })).toBe('#3.2');
  });
});
