import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type ColumnsListColumn = { title: string; items: string[] };

export type ColumnsListSlots = {
  kicker?: string;
  headline: string;
  columns: ColumnsListColumn[];
  emphasis?: Emphasis[];
};

export const KEYS = [
  'kicker',
  'headline',
  'columns',
  'emphasis',
] as const satisfies readonly (keyof ColumnsListSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<ColumnsListSlots, typeof KEYS>>;
