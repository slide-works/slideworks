import type { ChartSlots } from '../slots';

type Described = Pick<ChartSlots, 'kind' | 'xLabels' | 'series' | 'yUnit' | 'marker'>;

const NUMBER = new Intl.NumberFormat('en', { maximumFractionDigits: 20 });

// The chart's accessible name: its kind and its series.
export function chartTitle({ kind, series }: Described): string {
  return `${kind === 'bar' ? 'Bar' : 'Line'} chart: ${series.map((s) => s.name).join(', ')}`;
}

// The chart's data in plain text for a screen reader: each series' values
// against the x labels, the y unit and the marker.
export function chartDescription({ xLabels, series, yUnit, marker }: Described): string {
  const unit = yUnit !== undefined ? ` (${yUnit})` : '';
  const lines = series.map(
    (s) =>
      `${s.name}${unit}: ${s.values.map((v, i) => `${xLabels[i]} ${NUMBER.format(v)}`).join(', ')}.`,
  );
  if (marker !== undefined)
    lines.push(`Marker "${marker.label}" ${markerPlace(xLabels, marker.x)}.`);
  return lines.join(' ');
}

// x is 1-based on the labels: 3 is at the third, 3.5 between the third and fourth.
function markerPlace(xLabels: string[], x: number): string {
  const before = Math.floor(x);
  if (before === x) return `at ${xLabels[x - 1]}`;
  if (before < 1) return `before ${xLabels[0]}`;
  if (before >= xLabels.length) return `after ${xLabels[xLabels.length - 1]}`;
  return `between ${xLabels[before - 1]} and ${xLabels[before]}`;
}
