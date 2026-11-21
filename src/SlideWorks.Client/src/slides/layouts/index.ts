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
import { VideoLayout } from './VideoLayout';
import { TableLayout } from './TableLayout';
import { ChartLayout } from './ChartLayout';
import { GanttLayout } from './GanttLayout';
import { NumberedCardsLayout } from './NumberedCardsLayout';
import { RulesLayout } from './RulesLayout';
import { CardsLayout } from './CardsLayout';
import { StatementLayout } from './StatementLayout';
import { LinksLayout } from './LinksLayout';
import { StackLayout } from './StackLayout';
import { BarsLayout } from './BarsLayout';
import { FlowLayout } from './FlowLayout';
import { LevelsLayout } from './LevelsLayout';
import { ColumnsListLayout } from './ColumnsListLayout';
import { TimelineLayout } from './TimelineLayout';
import { LoopLayout } from './LoopLayout';
import { CloseLayout } from './CloseLayout';
import { JoinLayout } from './JoinLayout';
import { PollLayout } from './PollLayout';
import { QaLayout } from './QaLayout';
import { WordCloudLayout } from './WordCloudLayout';
import { OpenTextLayout } from './OpenTextLayout';

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
  video: VideoLayout,
  table: TableLayout,
  chart: ChartLayout,
  gantt: GanttLayout,
  'numbered-cards': NumberedCardsLayout,
  rules: RulesLayout,
  cards: CardsLayout,
  statement: StatementLayout,
  links: LinksLayout,
  stack: StackLayout,
  bars: BarsLayout,
  flow: FlowLayout,
  levels: LevelsLayout,
  'columns-list': ColumnsListLayout,
  timeline: TimelineLayout,
  loop: LoopLayout,
  close: CloseLayout,
  join: JoinLayout,
  poll: PollLayout,
  qa: QaLayout,
  'word-cloud': WordCloudLayout,
  'open-text': OpenTextLayout,
};
