import type {
  AssertTrue,
  ListsEveryRequiredKey,
  RequiredKey,
} from '../../../slides/slots/keyChecks';
import type { ColumnsListSlots } from '../../../slides/slots/columns-list';

export const SLOTS: ColumnsListSlots = {
  kicker: 'The list',
  headline: 'Three things the room takes home',
  columns: [
    { title: 'See', items: ['One customer this week'] },
    { title: 'Ship', items: ['One small change'] },
    { title: 'Learn', items: ['What they did next'] },
  ],
  emphasis: [{ text: 'takes home', tone: 'accent' }],
};

export const REQUIRED = [
  'headline',
  'columns',
] as const satisfies readonly RequiredKey<ColumnsListSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<ColumnsListSlots, typeof REQUIRED>>;
