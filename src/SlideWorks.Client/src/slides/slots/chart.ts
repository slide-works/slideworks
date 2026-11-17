// The chart layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type ChartSeries = { name: string; values: number[] };

export type ChartSlots = {
  kicker?: string;
  headline: string;
  kind: 'line' | 'bar';
  xLabels: string[];
  series: ChartSeries[];
  yUnit?: string;
  caption?: string;
  // x is 1-based on xLabels: 1 is the first label, 3.5 sits between the third and fourth.
  marker?: { x: number; label: string };
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'kind',
  'xLabels',
  'series',
  'yUnit',
  'caption',
  'marker',
] as const satisfies readonly (keyof ChartSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<ChartSlots, typeof KEYS>>;
