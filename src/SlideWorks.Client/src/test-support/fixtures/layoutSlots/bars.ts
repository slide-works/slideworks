import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { BarsSlots } from '../../../slides/slots/bars';

// One valid bars slide's slots, with every optional slot set.
export const SLOTS: BarsSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
  bars: [
    {
      label: 'Release',
      sub: 'Weeks to ship',
      value: 9,
      was: 12,
      note: 'One big drop',
      tone: 'negative',
      tag: 'Late',
    },
    { label: 'Review', value: 6 },
    { label: 'Build', value: 4, tone: 'positive' },
  ],
  unit: 'wks',
  values: 'shown',
  wasLabel: 'Last year',
  caption: 'Time from idea to customer',
  reveal: 'one-by-one',
  callout: { tone: 'accent', text: 'Cut the batch, not the corners.', reveal: 'last' },
  emphasis: [{ text: 'big batch', tone: 'negative' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'bars'] as const satisfies readonly RequiredKey<BarsSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<BarsSlots, typeof REQUIRED>>;
