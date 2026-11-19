import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type TimelineEvent = { when: string; text: string };

export type TimelineSlots = {
  kicker?: string;
  headline: string;
  events: TimelineEvent[];
  emphasis?: Emphasis[];
};

export const KEYS = [
  'kicker',
  'headline',
  'events',
  'emphasis',
] as const satisfies readonly (keyof TimelineSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<TimelineSlots, typeof KEYS>>;
