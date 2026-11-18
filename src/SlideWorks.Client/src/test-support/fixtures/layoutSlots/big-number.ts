import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { BigNumberSlots } from '../../../slides/slots/big-number';

// One valid big-number slide's slots, with every optional slot set.
export const SLOTS: BigNumberSlots = {
  kicker: 'The cost',
  headline: 'It took us eleven weeks to get a change in front of a customer',
  value: '11 wks',
  caption: 'Median from first commit to the first customer using it',
  detail: 'Eight of our 12 epics.',
  source: 'Source: delivery dashboard, Jan–Dec',
  reveal: 'one-by-one',
  callout: { tone: 'info', text: 'Cut the batch, not the scope.', reveal: 'last' },
  emphasis: [{ text: 'eleven weeks', tone: 'negative' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'value',
  'caption',
] as const satisfies readonly RequiredKey<BigNumberSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<BigNumberSlots, typeof REQUIRED>>;
