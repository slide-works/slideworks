import { describe, expect, it } from 'vitest';
import { chartDescription, chartTitle } from './describeChart';

const chart = {
  kind: 'bar' as const,
  xLabels: ['Q1', 'Q2', 'Q3'],
  series: [
    { name: 'Payments', values: [10, 8.5, 37500] },
    { name: 'Search', values: [-2, 0, 1_000_000_000] },
  ],
};

describe('chartTitle', () => {
  it('names the kind and the series', () => {
    expect(chartTitle(chart)).toBe('Bar chart: Payments, Search');
    expect(chartTitle({ ...chart, kind: 'line' })).toBe('Line chart: Payments, Search');
  });
});

describe('chartDescription', () => {
  it('lists every series value against its x label, with the unit', () => {
    expect(chartDescription({ ...chart, yUnit: 'days' })).toBe(
      'Payments (days): Q1 10, Q2 8.5, Q3 37,500. Search (days): Q1 -2, Q2 0, Q3 1,000,000,000.',
    );
  });

  it('places the marker on or between the x labels', () => {
    expect(chartDescription({ ...chart, marker: { x: 2, label: 'Flags on' } })).toMatch(
      / Marker "Flags on" at Q2\.$/,
    );
    expect(chartDescription({ ...chart, marker: { x: 2.5, label: 'Launch' } })).toMatch(
      / Marker "Launch" between Q2 and Q3\.$/,
    );
    expect(chartDescription({ ...chart, marker: { x: 0.5, label: 'Start' } })).toMatch(
      / Marker "Start" before Q1\.$/,
    );
    expect(chartDescription({ ...chart, marker: { x: 3.5, label: 'End' } })).toMatch(
      / Marker "End" after Q3\.$/,
    );
  });
});
