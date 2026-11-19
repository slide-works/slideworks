import type {
  AssertTrue,
  ListsEveryRequiredKey,
  RequiredKey,
} from '../../../slides/slots/keyChecks';
import type { TimelineSlots } from '../../../slides/slots/timeline';

export const SLOTS: TimelineSlots = {
  kicker: 'The week',
  headline: 'Monday to Friday, one change at a time',
  events: [
    { when: 'Monday', text: 'Pick the smallest change a customer can see.' },
    { when: 'Friday', text: 'Show them what shipped and ask what is next.' },
  ],
  emphasis: [{ text: 'one change', tone: 'accent' }],
};

export const REQUIRED = [
  'headline',
  'events',
] as const satisfies readonly RequiredKey<TimelineSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<TimelineSlots, typeof REQUIRED>>;
