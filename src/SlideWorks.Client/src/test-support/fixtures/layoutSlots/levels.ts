import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { LevelsSlots } from '../../../slides/slots/levels';

// One valid levels slide's slots, with every optional slot set.
export const SLOTS: LevelsSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
  levels: {
    title: 'Maturity',
    items: [
      { text: 'Continuous flow', mark: '4' },
      { text: 'Small batches', mark: '3', state: 'current' },
      { text: 'Quarterly releases', mark: '2' },
      { text: 'Big bang', mark: '1' },
    ],
  },
  main: {
    title: 'Where we are',
    icon: 'rocket',
    items: ['We ship weekly', { lead: 'Next', text: 'Ship daily behind flags.' }],
  },
  facts: [
    { label: 'Cycle time', value: '9 days', tone: 'accent' },
    { label: 'Slips', value: '2 a year' },
  ],
  side: 'right',
  callout: { tone: 'accent', text: 'Cut the batch, not the corners.', reveal: 'last' },
  emphasis: [{ text: 'big batch', tone: 'negative' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'levels',
  'main',
] as const satisfies readonly RequiredKey<LevelsSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<LevelsSlots, typeof REQUIRED>>;
