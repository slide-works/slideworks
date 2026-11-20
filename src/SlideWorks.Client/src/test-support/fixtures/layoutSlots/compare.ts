import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { CompareSlots } from '../../../slides/slots/compare';

// One valid compare slide's slots, with every optional slot set.
export const SLOTS: CompareSlots = {
  kicker: 'Two teams, one quarter',
  headline: 'The difference is when the customer gets to answer',
  failure: {
    label: 'Checkout',
    title: 'Sliced by layer',
    sub: 'One launch',
    points: ['Nothing to show for 11 weeks.'],
    stamp: 'Late',
    footer: 'Nothing shipped.',
    icon: 'coins',
  },
  success: {
    label: 'Search',
    title: 'Sliced by customer action',
    sub: 'Nine releases',
    rows: [{ label: 'Releases', value: 'Nine' }],
    stamp: 'Live',
    footer: 'Something shipped weekly.',
    tone: 'positive',
    fill: 'plain',
  },
  reveal: 'one-by-one',
  callout: { tone: 'info', text: 'Slice by what the customer does.', reveal: 'last' },
  emphasis: [{ text: 'when the customer', tone: 'accent', in: 'headline' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'failure',
  'success',
] as const satisfies readonly RequiredKey<CompareSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<CompareSlots, typeof REQUIRED>>;
