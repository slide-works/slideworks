import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { FlowSlots } from '../../../slides/slots/flow';

// One valid flow slide's slots, with every optional slot set.
export const SLOTS: FlowSlots = {
  kicker: 'How a record travels',
  headline: 'Every record takes the same short road to the archive',
  nodes: [
    {
      label: 'Step one',
      title: 'Validate',
      text: 'Checks each field and parks bad rows.',
      tone: 'accent',
    },
    {
      title: 'Clean',
      text: 'Fixes the rest in turn.',
      items: ['Verify signatures', 'Strip tracking ids'],
    },
    { title: 'Archive', text: 'Keeps a copy for ninety days.', tone: 'positive' },
  ],
  caption: 'Nothing moves on until the step before it is done',
  chips: ['Hourly', 'Retries twice'],
  reveal: 'one-by-one',
  callout: { tone: 'accent', text: 'Nothing skips a step.', reveal: 'last' },
  emphasis: [{ text: 'short road', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'nodes'] as const satisfies readonly RequiredKey<FlowSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<FlowSlots, typeof REQUIRED>>;
