import type {
  AssertTrue,
  ListsEveryRequiredKey,
  RequiredKey,
} from '../../../slides/slots/keyChecks';
import type { LoopSlots } from '../../../slides/slots/loop';

export const SLOTS: LoopSlots = {
  kicker: 'The cycle',
  headline: 'See it, ship it, learn, then see the next one',
  steps: [
    { label: 'See', text: 'Watch one customer use it.' },
    { label: 'Ship', text: 'Change one thing they felt.' },
    { label: 'Learn', text: 'Ask what they do next.' },
  ],
  emphasis: [{ text: 'ship it', tone: 'accent' }],
};

export const REQUIRED = ['headline', 'steps'] as const satisfies readonly RequiredKey<LoopSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<LoopSlots, typeof REQUIRED>>;
