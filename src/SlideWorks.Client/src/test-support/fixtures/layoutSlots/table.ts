import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { TableSlots } from '../../../slides/slots/table';

// One valid table slide's slots, with every optional slot set.
export const SLOTS: TableSlots = {
  kicker: 'Ship it',
  headline: 'Aim for slices a pair can ship inside a week',
  columns: ['Slice', 'Size', 'Status'],
  rows: [
    ['Save a search', 'S', { text: 'Shipped', tone: 'good' }],
    ['Share a search', 'M', { text: 'Late', tone: 'warn' }],
  ],
  highlightRow: 1,
  reveal: 'all',
  callout: { tone: 'info', text: 'Two slices are already live.' },
  emphasis: [{ text: 'a week', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'columns',
  'rows',
] as const satisfies readonly RequiredKey<TableSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<TableSlots, typeof REQUIRED>>;
