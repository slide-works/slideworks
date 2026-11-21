import { describe, expect, it } from 'vitest';
import type { SlideFinding } from '../../api/types';
import { mergeFindings } from './useSlideChecks';

const finding = (
  slide: number,
  rule: SlideFinding['rule'],
  severity: SlideFinding['severity'],
  source: SlideFinding['source'],
): SlideFinding => ({
  slide,
  rule,
  severity,
  source,
  where: 'body',
  message: `${rule} on ${slide}`,
});

describe('mergeFindings', () => {
  it('merges by slide and sorts by severity', () => {
    const stored = [finding(2, 'genericAlt', 'warning', 'stored')];
    const measured = [
      finding(2, 'textOnImage', 'note', 'measured'),
      finding(2, 'overflow', 'error', 'measured'),
      finding(1, 'smallText', 'warning', 'measured'),
      finding(2, 'smallText', 'warning', 'measured'),
    ];
    const bySlide = mergeFindings(stored, measured);
    expect([...bySlide.keys()].sort()).toEqual([1, 2]);
    expect(bySlide.get(2)?.map((f) => f.rule)).toEqual([
      'overflow',
      'smallText',
      'genericAlt',
      'textOnImage',
    ]);
  });
});
