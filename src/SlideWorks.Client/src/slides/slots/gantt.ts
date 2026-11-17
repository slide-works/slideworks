// The gantt layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type GanttStatus = 'done' | 'slips' | 'stopped' | 'ship';

// start and end are 1-based column numbers, end inclusive.
export type GanttRow = {
  label: string;
  start: number;
  end: number;
  status: GanttStatus;
  note?: string;
};

export type GanttSlots = {
  kicker?: string;
  headline: string;
  columns: string[];
  rows: GanttRow[];
  caption?: string;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'columns',
  'rows',
  'caption',
] as const satisfies readonly (keyof GanttSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<GanttSlots, typeof KEYS>>;
