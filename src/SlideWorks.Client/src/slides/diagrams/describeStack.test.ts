import { describe, expect, it } from 'vitest';
import { describeStack } from './describeStack';

describe('describeStack', () => {
  it('names the segments, values and unit', () => {
    expect(
      describeStack({
        segments: [
          { label: 'Release', value: 4.5, tag: 'Late', note: 'One big drop' },
          { label: 'Plan', value: 1 },
        ],
        unit: 'wks',
        total: '5.5 weeks',
      }),
    ).toBe(
      'Stack of 2 segments, top to bottom: Release 4.5 wks (Late), One big drop; Plan 1 wks, making 5.5 weeks.',
    );
  });

  it('names only the labels when there are no values', () => {
    expect(describeStack({ segments: [{ label: 'A' }, { label: 'B' }] })).toBe(
      'Stack of 2 segments, top to bottom: A; B.',
    );
  });
});
