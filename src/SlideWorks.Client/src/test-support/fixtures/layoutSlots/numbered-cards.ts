import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { NumberedCardsSlots } from '../../../slides/slots/numbered-cards';

// One valid numbered-cards slide's slots, with every optional slot set.
export const SLOTS: NumberedCardsSlots = {
  kicker: 'Make it stick',
  headline: 'Four questions, in this order',
  cards: [
    { title: 'Who notices?', text: 'Name the customer.' },
    { title: 'What can they do?', text: 'One sentence.' },
  ],
  reveal: 'one-by-one',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'cards',
] as const satisfies readonly RequiredKey<NumberedCardsSlots>[];
export type RequiredComplete = AssertTrue<
  ListsEveryRequiredKey<NumberedCardsSlots, typeof REQUIRED>
>;
