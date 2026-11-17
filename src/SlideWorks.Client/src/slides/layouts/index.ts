// The layout registry (K12), frozen: one component per layout name. A layout's
// task fills in its own <Name>Layout.tsx and never edits this file.
import type { ComponentType } from 'react';
import type { LayoutName, LayoutProps } from '../slots';
import { TitleLayout } from './TitleLayout';
import { DividerLayout } from './DividerLayout';
import { BulletsLayout } from './BulletsLayout';
import { TwoColumnLayout } from './TwoColumnLayout';
import { PracticeLayout } from './PracticeLayout';
import { CompareLayout } from './CompareLayout';
import { BigNumberLayout } from './BigNumberLayout';
import { QuoteLayout } from './QuoteLayout';
import { ImageLayout } from './ImageLayout';
import { TableLayout } from './TableLayout';
import { ChartLayout } from './ChartLayout';
import { GanttLayout } from './GanttLayout';
import { NumberedCardsLayout } from './NumberedCardsLayout';
import { RulesLayout } from './RulesLayout';
import { CardsLayout } from './CardsLayout';
import { CloseLayout } from './CloseLayout';
import { JoinLayout } from './JoinLayout';
import { PollLayout } from './PollLayout';

export const LAYOUTS: { [L in LayoutName]: ComponentType<LayoutProps<L>> } = {
  title: TitleLayout,
  divider: DividerLayout,
  bullets: BulletsLayout,
  'two-column': TwoColumnLayout,
  practice: PracticeLayout,
  compare: CompareLayout,
  'big-number': BigNumberLayout,
  quote: QuoteLayout,
  image: ImageLayout,
  table: TableLayout,
  chart: ChartLayout,
  gantt: GanttLayout,
  'numbered-cards': NumberedCardsLayout,
  rules: RulesLayout,
  cards: CardsLayout,
  close: CloseLayout,
  join: JoinLayout,
  poll: PollLayout,
};
