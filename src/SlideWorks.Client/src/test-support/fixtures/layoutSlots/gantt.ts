import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { GanttSlots } from '../../../slides/slots/gantt';

// One valid gantt slide's slots, with every optional slot set.
export const SLOTS: GanttSlots = {
  kicker: 'The cost',
  headline: 'One milestone each looks fair',
  columns: ['S1', 'S2', 'S3', 'S4'],
  rows: [
    { label: 'Checkout API', start: 1, end: 2, status: 'done' },
    { label: 'Checkout UI', start: 2, end: 4, status: 'slips', note: 'Waited on the API' },
  ],
  caption: 'Everything is green until it isn’t.',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'columns',
  'rows',
] as const satisfies readonly RequiredKey<GanttSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<GanttSlots, typeof REQUIRED>>;
