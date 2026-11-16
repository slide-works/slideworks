import { describe, expect, it } from 'vitest';
import { formatValue, linear, niceStep, niceTicks, tickLabels } from './scale';

describe('niceTicks', () => {
  it('covers 0 to 11 in steps of 2', () => {
    expect(niceTicks(0, 11)).toEqual({ lo: 0, hi: 12, step: 2, values: [0, 2, 4, 6, 8, 10, 12] });
  });

  it('starts at or below a minimum that is not on a step', () => {
    const ticks = niceTicks(13, 87);
    expect(ticks.step).toBe(10);
    expect(ticks.values).toEqual([10, 20, 30, 40, 50, 60, 70, 80, 90]);
  });

  it('spans negative and positive values through zero', () => {
    const ticks = niceTicks(-35, 60);
    expect(ticks.values).toContain(0);
    expect(ticks.lo).toBeLessThanOrEqual(-35);
    expect(ticks.hi).toBeGreaterThanOrEqual(60);
    expect(ticks.step).toBe(20);
  });

  it('takes fewer, larger steps for a smaller target', () => {
    expect(niceTicks(0, 11, 3).values).toEqual([0, 5, 10, 15]);
  });

  it('keeps fractional ticks free of float noise', () => {
    expect(niceTicks(0, 0.7).values).toEqual([0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7]);
  });

  it('reaches the limits of the value range', () => {
    const ticks = niceTicks(-1_000_000_000, 1_000_000_000);
    expect(ticks.lo).toBe(-1_000_000_000);
    expect(ticks.hi).toBe(1_000_000_000);
    expect(ticks.step).toBe(500_000_000);
  });

  it('widens equal ends so the axis has a range', () => {
    expect(niceTicks(0, 0)).toEqual({
      lo: 0,
      hi: 1,
      step: 0.2,
      values: [0, 0.2, 0.4, 0.6, 0.8, 1],
    });
    const flat = niceTicks(5, 5);
    expect(flat.lo).toBeLessThan(5);
    expect(flat.hi).toBeGreaterThan(5);
  });

  it('refuses a minimum above the maximum', () => {
    expect(() => niceTicks(3, 1)).toThrow(RangeError);
  });
});

describe('niceStep', () => {
  it('rounds to the nearest 1, 2, 5 or 10 times a power of ten', () => {
    expect([0.7, 1.5, 2.2, 3, 7, 0.013].map(niceStep)).toEqual([1, 2, 2, 5, 10, 0.01]);
  });
});

describe('linear', () => {
  it('maps the domain onto the range, inverted for SVG y', () => {
    const y = linear(0, 12, 490, 40);
    expect(y(0)).toBe(490);
    expect(y(12)).toBe(40);
    expect(y(6)).toBe(265);
  });
});

describe('formatValue', () => {
  it('writes compact labels', () => {
    expect(
      [11, 2500, 1_200_000, -1_000_000_000, 0.25, 0.30000000000000004].map(formatValue),
    ).toEqual(['11', '2.5K', '1.2M', '-1B', '0.25', '0.3']);
  });
});

describe('tickLabels', () => {
  // Every tick label on an axis is different, whatever the range.
  it.each([
    ['a narrow band at large magnitude', 101_400, 102_300],
    ['a narrow band in the millions', 4_120_000, 4_190_000],
    ['a narrow band near the top of the range', 999_999_000, 1_000_000_000],
    ['the whole range', -1_000_000_000, 1_000_000_000],
    ['small fractions', 0.001, 0.0042],
    ['tenths', 0, 0.7],
    ['negative values', -0.9, -0.2],
    ['negative thousands', -102_300, -101_400],
    ['a range crossing zero', -35, 60],
    ['thousands crossing zero', -1_500, 2_250],
    ['a step of a fraction of the unit', 1_000, 1_001],
  ])('are unique for %s', (_, min, max) => {
    const labels = tickLabels(niceTicks(min, max));
    expect(new Set(labels).size).toBe(labels.length);
  });

  it('take their precision from the step', () => {
    expect(tickLabels(niceTicks(101_400, 102_300))).toEqual([
      '101.4K',
      '101.6K',
      '101.8K',
      '102K',
      '102.2K',
      '102.4K',
    ]);
    expect(tickLabels(niceTicks(-1_000_000_000, 1_000_000_000))).toEqual([
      '-1B',
      '-0.5B',
      '0',
      '0.5B',
      '1B',
    ]);
    expect(tickLabels(niceTicks(0, 11))).toEqual(['0', '2', '4', '6', '8', '10', '12']);
    expect(tickLabels(niceTicks(-35, 60))).toEqual(['-40', '-20', '0', '20', '40', '60']);
  });

  it('stay in plain numbers when the step is tiny for the unit', () => {
    expect(tickLabels(niceTicks(1_000, 1_001))).toEqual([
      '1,000',
      '1,000.2',
      '1,000.4',
      '1,000.6',
      '1,000.8',
      '1,001',
    ]);
  });
});
