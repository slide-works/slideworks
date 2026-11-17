// The compare layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Fill, Tone } from './shared';

// failure defaults to the negative tone and success to the positive one.
export type CompareSide = {
  label: string;
  title: string;
  sub?: string;
  points: string[];
  tone?: Tone;
  fill?: Fill;
};

export type CompareSlots = {
  kicker?: string;
  headline: string;
  failure: CompareSide;
  success: CompareSide;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'failure',
  'success',
] as const satisfies readonly (keyof CompareSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<CompareSlots, typeof KEYS>>;
