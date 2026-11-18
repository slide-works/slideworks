// The flow layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal, Tone } from './shared';

// One node, left to right. `items` (2–4, one node only) draws it as a box of tiles.
export type FlowNode = {
  title: string;
  label?: string;
  text?: string;
  items?: string[];
  // inverse and accent fill the card; positive and negative colour its rule.
  tone?: Tone;
};

export type FlowSlots = {
  kicker?: string;
  headline: string;
  nodes: FlowNode[];
  caption?: string;
  chips?: string[];
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'nodes',
  'caption',
  'chips',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof FlowSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<FlowSlots, typeof KEYS>>;
