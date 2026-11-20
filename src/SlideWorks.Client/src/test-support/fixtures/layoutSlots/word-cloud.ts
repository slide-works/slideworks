import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { WordCloudSlots } from '../../../slides/slots/word-cloud';

// One valid word-cloud slide's slots, with every optional slot set.
export const SLOTS: WordCloudSlots = { question: 'One word for how this sprint felt?' };

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['question'] as const satisfies readonly RequiredKey<WordCloudSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<WordCloudSlots, typeof REQUIRED>>;
