// The image layout's slots (K5). Field names are frozen; the server sets the limits.
import type { ImageRef } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type ImageSlots = {
  kicker?: string;
  headline: string;
  image: ImageRef;
  points?: string[];
  caption?: string;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'image',
  'points',
  'caption',
] as const satisfies readonly (keyof ImageSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<ImageSlots, typeof KEYS>>;
