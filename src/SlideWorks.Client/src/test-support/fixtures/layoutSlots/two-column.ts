import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { TwoColumnSlots } from '../../../slides/slots/two-column';
import { SAMPLE_IMAGE_ID } from './sampleImage';

// One valid two-column slide's slots, with every optional slot set.
export const SLOTS: TwoColumnSlots = {
  kicker: 'Slice it',
  headline: 'A story map shows the thinnest release',
  left: { title: 'How to read it', items: ['One row is one release.'] },
  right: { image: { image: SAMPLE_IMAGE_ID, alt: 'A story map' } },
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'left',
  'right',
] as const satisfies readonly RequiredKey<TwoColumnSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<TwoColumnSlots, typeof REQUIRED>>;
