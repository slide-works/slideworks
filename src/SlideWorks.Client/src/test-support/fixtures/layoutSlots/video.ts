import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { VideoSlots } from '../../../slides/slots/video';

// One valid video slide's slots, with every optional slot set.
export const SLOTS: VideoSlots = {
  kicker: 'The demo',
  headline: 'The change is easier to see than to describe',
  url: 'https://www.youtube.com/watch?v=M7lc1UVf-VE',
  start: 12,
  play: 'click',
  caption: 'A two minute walkthrough of the slice.',
  emphasis: [{ text: 'easier to see', tone: 'accent' }],
};

export const REQUIRED = ['headline', 'url'] as const satisfies readonly RequiredKey<VideoSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<VideoSlots, typeof REQUIRED>>;
