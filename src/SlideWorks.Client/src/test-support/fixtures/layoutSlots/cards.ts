import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { CardsSlots } from '../../../slides/slots/cards';

// One valid cards slide's slots, with every optional slot set.
export const SLOTS: CardsSlots = {
  kicker: 'Make it stick',
  headline: 'Everyone changes one habit',
  cards: [
    {
      badge: 'PM',
      title: 'Write the demo sentence',
      text: 'Before the ticket.',
      chips: ['Planning'],
    },
  ],
  reveal: 'all',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'cards'] as const satisfies readonly RequiredKey<CardsSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<CardsSlots, typeof REQUIRED>>;
