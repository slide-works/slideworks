import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { StackSlots } from '../../../slides/slots/stack';

// One valid stack slide's slots, with every optional slot set.
export const SLOTS: StackSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
  segments: [
    { label: 'Release', value: 4, note: 'One big drop', tone: 'negative', tag: 'Late' },
    { label: 'Review', value: 3 },
    { label: 'Build', value: 2 },
    { label: 'Plan', value: 1 },
  ],
  unit: 'wks',
  total: 'Ten weeks end to end',
  caption: 'Time from idea to customer',
  points: [
    'Small batches shorten every step',
    { lead: 'Faster', text: 'Feedback arrives sooner.' },
  ],
  side: 'right',
  reveal: 'one-by-one',
  callout: { tone: 'accent', text: 'Cut the batch, not the corners.', reveal: 'last' },
  emphasis: [{ text: 'big batch', tone: 'negative' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'segments',
] as const satisfies readonly RequiredKey<StackSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<StackSlots, typeof REQUIRED>>;
