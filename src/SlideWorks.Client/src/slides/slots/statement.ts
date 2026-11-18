// The statement layout's slots (K5). Field names are frozen; the server sets the limits.
import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

// statement may carry \n line breaks; it is the only slot that does. With reveal
// 'one-by-one' the sub arrives on step 1.
export type StatementSlots = {
  kicker?: string;
  statement: string;
  sub?: string;
  reveal?: Reveal;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'statement',
  'sub',
  'reveal',
  'emphasis',
] as const satisfies readonly (keyof StatementSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<StatementSlots, typeof KEYS>>;
