import { describe, expect, it } from 'vitest';
import { largestFit } from './useFitText';

// Fits when the size is at most `limit`, recording every size it measured.
function probe(limit: number) {
  const tried: number[] = [];
  const fits = (size: number) => {
    tried.push(size);
    return size <= limit;
  };
  return { tried, fits };
}

describe('largestFit', () => {
  it('keeps the start size when it fits, measuring once', () => {
    const { tried, fits } = probe(200);
    expect(largestFit(168, 96, fits)).toBe(168);
    expect(tried).toEqual([168]);
  });

  it.each([167, 131, 97, 96])('finds the largest size that fits (%i)', (limit) => {
    expect(largestFit(168, 96, probe(limit).fits)).toBe(limit);
  });

  it('stops at the floor when nothing fits', () => {
    expect(largestFit(168, 96, probe(40).fits)).toBe(96);
  });

  it('measures about log2 of the range, not once per pixel', () => {
    const { tried, fits } = probe(121);
    largestFit(300, 120, fits);
    expect(tried.length).toBeLessThanOrEqual(10);
  });
});
