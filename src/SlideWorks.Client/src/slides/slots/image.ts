// The image layout's slots (K5). Field names are frozen; the server sets the limits.
import type { BulletItem, CalloutBlock, Emphasis, ImageRef } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

export type ImageSlots = {
  kicker?: string;
  headline: string;
  image: ImageRef;
  // 'card' (the default) puts the image on a white card; 'none' draws it as it is.
  frame?: 'card' | 'none';
  // 'below' (the default) puts the image under the headline; 'right' beside it.
  position?: 'below' | 'right';
  points?: BulletItem[];
  caption?: string;
  // An https address. A YouTube or Vimeo address plays on the slide; any other opens from the image.
  link?: string;
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'image',
  'frame',
  'position',
  'points',
  'caption',
  'link',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof ImageSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<ImageSlots, typeof KEYS>>;
