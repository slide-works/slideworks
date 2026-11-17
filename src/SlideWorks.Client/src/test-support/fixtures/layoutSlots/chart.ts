import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { ChartSlots } from '../../../slides/slots/chart';

// One valid chart slide's slots, with every optional slot set.
export const SLOTS: ChartSlots = {
  kicker: 'Ship it',
  headline: 'Lead time fell as the slices got smaller',
  kind: 'bar',
  xLabels: ['Jul', 'Aug', 'Sep', 'Oct'],
  series: [{ name: 'Lead time (days)', values: [24, 18, 9, 6] }],
  yUnit: 'days',
  caption: 'Median lead time per month.',
  marker: { x: 2.5, label: 'Flags on' },
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'kind',
  'xLabels',
  'series',
] as const satisfies readonly RequiredKey<ChartSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<ChartSlots, typeof REQUIRED>>;
