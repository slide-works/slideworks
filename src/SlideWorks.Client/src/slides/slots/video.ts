// The video layout's slots. Field names are frozen; the server sets the limits.
import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type VideoSlots = {
  kicker?: string;
  headline: string;
  // A YouTube watch, youtu.be, embed, shorts or live link.
  url: string;
  // Seconds from the start. When set, it overrides a time in the link.
  start?: number;
  // 'click' (the default) plays when someone presses the video; 'auto' plays when the slide is shown.
  play?: 'click' | 'auto';
  caption?: string;
  emphasis?: Emphasis[];
};

export const KEYS = [
  'kicker',
  'headline',
  'url',
  'start',
  'play',
  'caption',
  'emphasis',
] as const satisfies readonly (keyof VideoSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<VideoSlots, typeof KEYS>>;
