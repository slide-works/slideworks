import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { BulletsSlots } from '../../../slides/slots/bullets';

// One valid bullets slide's slots, with every optional slot set.
export const SLOTS: BulletsSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
  items: [
    'Risk shows up late.',
    { lead: 'Merges get scary.', text: 'Long branches collide.' },
    { icon: 'bolt', text: 'Small slices ship fast.' },
  ],
  style: 'markers',
  reveal: 'one-by-one',
  callout: { tone: 'info', text: 'Nobody can say no to a slice.', reveal: 'last' },
  emphasis: [{ text: 'big batch', tone: 'negative' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'items',
] as const satisfies readonly RequiredKey<BulletsSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<BulletsSlots, typeof REQUIRED>>;
