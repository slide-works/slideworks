import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type LoopStep = { label: string; text: string };

export type LoopSlots = {
  kicker?: string;
  headline: string;
  steps: LoopStep[];
  emphasis?: Emphasis[];
};

export const KEYS = [
  'kicker',
  'headline',
  'steps',
  'emphasis',
] as const satisfies readonly (keyof LoopSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<LoopSlots, typeof KEYS>>;
