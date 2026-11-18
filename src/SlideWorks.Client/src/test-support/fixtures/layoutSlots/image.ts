import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { ImageSlots } from '../../../slides/slots/image';
import { SAMPLE_IMAGE_ID } from './sampleImage';

// One valid image slide's slots, with every optional slot set.
export const SLOTS: ImageSlots = {
  kicker: 'Slice it',
  headline: 'The story map',
  image: { image: SAMPLE_IMAGE_ID, alt: 'A story map' },
  frame: 'card',
  position: 'below',
  points: ['One row is one release.'],
  caption: 'The first row shipped in a week.',
  reveal: 'all',
  callout: { tone: 'info', text: 'Read it left to right.' },
  emphasis: [{ text: 'story map', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'image'] as const satisfies readonly RequiredKey<ImageSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<ImageSlots, typeof REQUIRED>>;
