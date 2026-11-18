import { describe, expect, it } from 'vitest';
import { describeBars } from './describeBars';

describe('describeBars', () => {
  it('names the bars, values, unit, was, tag and note', () => {
    expect(
      describeBars({
        bars: [
          { label: 'Release', value: 4.5, was: 9, tag: 'Late', note: 'One big drop' },
          { label: 'Plan', value: 1200 },
        ],
        unit: 'wks',
        wasLabel: 'Last year',
      }),
    ).toBe(
      'Bars comparing 2 things: Release 4.5 wks, Last year 9 (Late), One big drop; Plan 1,200 wks.',
    );
  });

  it('leaves the numbers off when they are hidden', () => {
    expect(
      describeBars({
        bars: [
          { label: 'A', value: 1 },
          { label: 'B', value: 2 },
        ],
        unit: 'ms',
        values: 'hidden',
      }),
    ).toBe('Bars comparing 2 things: A; B.');
  });
});
