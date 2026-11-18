import { describe, expect, it } from 'vitest';
import {
  endWidth,
  MIN_BAR,
  NOTE_FONT,
  NOTE_GAP,
  NOTE_PAD,
  noteWidth,
  planBars,
  TRACK,
  barHeight,
  rowGap,
} from './barsGeometry';

describe('barsGeometry', () => {
  it('scales to the largest value or was', () => {
    const plan = planBars(
      [
        { label: 'A', value: 50, was: 200 },
        { label: 'B', value: 100 },
      ],
      undefined,
      false,
    );
    expect(plan.available).toBe(TRACK);
    expect(plan.placements[0].outline).toBeCloseTo(TRACK, 5);
    expect(plan.placements[0].length).toBeCloseTo(TRACK / 4, 5);
    expect(plan.placements[1].length).toBeCloseTo(TRACK / 2, 5);
  });

  it('keeps room for the widest end label', () => {
    const bars = [
      { label: 'A', value: 10, tag: 'Bottleneck' },
      { label: 'B', value: 4 },
    ];
    const widest = endWidth(bars[0], 'ms', true);
    // "10 ms" is 5 characters at 28 px x 0.62, the tag 10 at 18 px x 0.85 + 14, and 30.
    expect(widest).toBeCloseTo(5 * 28 * 0.62 + (10 * 18 * 0.85 + 14) + 30, 5);
    const plan = planBars(bars, 'ms', true);
    expect(plan.available).toBeCloseTo(TRACK - widest, 5);
    expect(plan.placements[0].length).toBeCloseTo(TRACK - widest, 5);
  });

  it('places a long note after the bar', () => {
    const note = 'Fits only after the short one';
    const plan = planBars(
      [
        { label: 'A', value: 100 },
        { label: 'B', value: 10, note },
        { label: 'C', value: 100, note: 'Fits in' },
      ],
      undefined,
      false,
    );
    expect(noteWidth(note)).toBeCloseTo(note.length * NOTE_FONT * 0.56, 5);
    expect(plan.placements[1].length + 0).toBeLessThan(noteWidth(note) + NOTE_PAD);
    expect(plan.placements[1].note).toBe('after');
    expect(plan.placements[1].endLeft).toBeCloseTo(plan.placements[1].length + NOTE_GAP, 5);
    expect(plan.placements[2].note).toBe('inside');
  });

  it('a zero bar is 6 px', () => {
    const plan = planBars(
      [
        { label: 'A', value: 0 },
        { label: 'B', value: 5 },
      ],
      undefined,
      false,
    );
    expect(plan.placements[0].length).toBe(MIN_BAR);
  });

  it('sizes bars and rows by count', () => {
    expect([2, 3, 4, 5, 6].map(barHeight)).toEqual([96, 96, 80, 64, 56]);
    expect([2, 4, 5, 6].map(rowGap)).toEqual([28, 28, 20, 20]);
  });
});
