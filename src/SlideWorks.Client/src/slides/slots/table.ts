// The table layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// A toned cell is drawn as a badge; table cells also have a warning tone.
export type CellTone = 'good' | 'warn' | 'bad' | 'info';
export type TableCell = string | { text: string; tone: CellTone };

// 'one-by-one' shows one row per step; 'strike-and-answer' (two columns) shows
// every left cell and, one row per step, strikes it and shows its answer.
export type TableReveal = 'all' | 'one-by-one' | 'strike-and-answer';

export type TableSlots = {
  kicker?: string;
  headline: string;
  columns: string[];
  rows: TableCell[][];
  // The 1-based number of the row to highlight.
  highlightRow?: number;
  reveal?: TableReveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'columns',
  'rows',
  'highlightRow',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof TableSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<TableSlots, typeof KEYS>>;
