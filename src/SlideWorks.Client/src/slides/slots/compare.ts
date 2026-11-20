// The compare layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { CalloutBlock, Emphasis, PanelFill } from './blocks';
import type { Reveal, Tone } from './shared';

export type CompareRow = { label: string; value: string };

// failure defaults to the negative tone and success to the positive one. Exactly
// one of points, rows and line is set; the server checks it. `icon` is a name
// from icons.json; `stamp` is a short word stamped in the panel.
export type CompareSide = {
  label: string;
  title: string;
  sub?: string;
  points?: string[];
  rows?: CompareRow[];
  line?: string;
  stamp?: string;
  footer?: string;
  tone?: Tone;
  fill?: PanelFill;
  icon?: string;
};

export type CompareSlots = {
  kicker?: string;
  headline: string;
  failure: CompareSide;
  success: CompareSide;
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'failure',
  'success',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof CompareSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<CompareSlots, typeof KEYS>>;
