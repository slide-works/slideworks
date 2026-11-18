// The stack layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal, Tone } from './shared';

// One segment, listed top to bottom. `value` is on every segment or on none.
export type StackSegment = {
  label: string;
  value?: number;
  note?: string;
  tone?: Tone;
  tag?: string;
};

// A point beside the stack: text, or a large lead and its text.
export type StackPoint = string | { lead: string; text: string };

export type StackSlots = {
  kicker?: string;
  headline: string;
  segments: StackSegment[];
  // Only with values.
  unit?: string;
  total?: string;
  caption?: string;
  points?: StackPoint[];
  // The side the stack is on: 'right' (the default) or 'left'.
  side?: 'right' | 'left';
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'segments',
  'unit',
  'total',
  'caption',
  'points',
  'side',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof StackSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<StackSlots, typeof KEYS>>;
