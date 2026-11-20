// The slot types for each layout (SW-1 plan section 4, contract K2). Layout and
// field names are frozen; the server's SlideValidator sets the limits. Slot text
// is plain text: never parse it as Markdown or HTML (D12).
//
// Each layout's slots type and its key list live in slots/<layout>.ts (K5); this
// file re-exports them and holds what is about every layout at once.

import type { AssertTrue } from './slots/keyChecks';
import { KEYS as TITLE_KEYS, type TitleSlots } from './slots/title';
import { KEYS as DIVIDER_KEYS, type DividerSlots } from './slots/divider';
import { KEYS as BULLETS_KEYS, type BulletsSlots } from './slots/bullets';
import { KEYS as TWO_COLUMN_KEYS, type TwoColumnSlots } from './slots/two-column';
import { KEYS as PRACTICE_KEYS, type PracticeSlots } from './slots/practice';
import { KEYS as COMPARE_KEYS, type CompareSlots } from './slots/compare';
import { KEYS as BIG_NUMBER_KEYS, type BigNumberSlots } from './slots/big-number';
import { KEYS as QUOTE_KEYS, type QuoteSlots } from './slots/quote';
import { KEYS as IMAGE_KEYS, type ImageSlots } from './slots/image';
import { KEYS as VIDEO_KEYS, type VideoSlots } from './slots/video';
import { KEYS as TABLE_KEYS, type TableSlots } from './slots/table';
import { KEYS as CHART_KEYS, type ChartSlots } from './slots/chart';
import { KEYS as GANTT_KEYS, type GanttSlots } from './slots/gantt';
import { KEYS as NUMBERED_CARDS_KEYS, type NumberedCardsSlots } from './slots/numbered-cards';
import { KEYS as RULES_KEYS, type RulesSlots } from './slots/rules';
import { KEYS as CARDS_KEYS, type CardsSlots } from './slots/cards';
import { KEYS as STATEMENT_KEYS, type StatementSlots } from './slots/statement';
import { KEYS as LINKS_KEYS, type LinksSlots } from './slots/links';
import { KEYS as STACK_KEYS, type StackSlots } from './slots/stack';
import { KEYS as BARS_KEYS, type BarsSlots } from './slots/bars';
import { KEYS as FLOW_KEYS, type FlowSlots } from './slots/flow';
import { KEYS as LEVELS_KEYS, type LevelsSlots } from './slots/levels';
import { KEYS as COLUMNS_LIST_KEYS, type ColumnsListSlots } from './slots/columns-list';
import { KEYS as TIMELINE_KEYS, type TimelineSlots } from './slots/timeline';
import { KEYS as LOOP_KEYS, type LoopSlots } from './slots/loop';
import { KEYS as CLOSE_KEYS, type CloseSlots } from './slots/close';
import { KEYS as JOIN_KEYS, type JoinSlots } from './slots/join';
import { KEYS as POLL_KEYS, type PollSlots } from './slots/poll';
import { KEYS as QA_KEYS, type QaSlots } from './slots/qa';

export type {
  BulletItem,
  CalloutBlock,
  ColumnSlot,
  Emphasis,
  EmphasisTone,
  FactsBlock,
  ImageRef,
  LeadText,
  LevelsBlock,
  PanelFill,
  TitleText,
} from './slots/blocks';
export type { DeepDive, Fill, LayoutProps, Reveal, SlideDeck, Tone } from './slots/shared';
export type { TitleSlots } from './slots/title';
export type { DividerSlots } from './slots/divider';
export type { BulletsSlots } from './slots/bullets';
export type { TwoColumnSlots } from './slots/two-column';
export type { PracticeSlots } from './slots/practice';
export type { CompareSide, CompareSlots } from './slots/compare';
export type { BigNumberSlots } from './slots/big-number';
export type { QuoteSlots } from './slots/quote';
export type { ImageSlots } from './slots/image';
export type { VideoSlots } from './slots/video';
export type { CellTone, TableCell, TableSlots } from './slots/table';
export type { ChartSeries, ChartSlots } from './slots/chart';
export type { GanttRow, GanttSlots, GanttStatus } from './slots/gantt';
export type { NumberedCardsSlots } from './slots/numbered-cards';
export type { RulesSlots } from './slots/rules';
export type { Card, CardsSlots } from './slots/cards';
export type { StatementSlots } from './slots/statement';
export type { LinksSlots } from './slots/links';
export type { StackSlots } from './slots/stack';
export type { BarsSlots } from './slots/bars';
export type { FlowSlots } from './slots/flow';
export type { LevelsSlots } from './slots/levels';
export type { ColumnsListSlots } from './slots/columns-list';
export type { TimelineSlots } from './slots/timeline';
export type { LoopSlots } from './slots/loop';
export type { CloseSlots } from './slots/close';
export type { JoinSlots } from './slots/join';
export type { PollSlots } from './slots/poll';
export type { QaSlots } from './slots/qa';

export const LAYOUT_NAMES = [
  'title',
  'divider',
  'bullets',
  'two-column',
  'practice',
  'compare',
  'big-number',
  'quote',
  'image',
  'video',
  'table',
  'chart',
  'gantt',
  'numbered-cards',
  'rules',
  'cards',
  'statement',
  'links',
  'stack',
  'bars',
  'flow',
  'levels',
  'columns-list',
  'timeline',
  'loop',
  'close',
  'join',
  'poll',
  'qa',
] as const;

export type LayoutName = (typeof LAYOUT_NAMES)[number];

// A slide's surface: the light one or the night one.
export type Surface = 'light' | 'dark';

// Each layout's surface (K6): always light, always dark, or either, where the
// slide picks and light is what it gets when it doesn't.
export const LAYOUT_SURFACE: Record<LayoutName, Surface | 'either'> = {
  title: 'dark',
  divider: 'dark',
  bullets: 'either',
  'two-column': 'either',
  practice: 'either',
  compare: 'light',
  'big-number': 'either',
  quote: 'either',
  image: 'light',
  video: 'light',
  table: 'light',
  chart: 'light',
  gantt: 'light',
  'numbered-cards': 'light',
  rules: 'light',
  cards: 'either',
  statement: 'either',
  links: 'light',
  stack: 'light',
  bars: 'light',
  flow: 'light',
  levels: 'light',
  'columns-list': 'light',
  timeline: 'light',
  loop: 'light',
  close: 'dark',
  join: 'dark',
  poll: 'light',
  qa: 'light',
};

// The layouts the frame draws without a tracker or a footer, whatever their surface.
export const CHROMELESS_LAYOUTS: readonly LayoutName[] = ['title', 'divider', 'close', 'join'];

// The layouts that keep the footer but draw no tracker (K16).
export const NO_TRACKER_LAYOUTS: readonly LayoutName[] = ['statement'];

export type Slots = {
  title: TitleSlots;
  divider: DividerSlots;
  bullets: BulletsSlots;
  'two-column': TwoColumnSlots;
  practice: PracticeSlots;
  compare: CompareSlots;
  'big-number': BigNumberSlots;
  quote: QuoteSlots;
  image: ImageSlots;
  video: VideoSlots;
  table: TableSlots;
  chart: ChartSlots;
  gantt: GanttSlots;
  'numbered-cards': NumberedCardsSlots;
  rules: RulesSlots;
  cards: CardsSlots;
  statement: StatementSlots;
  links: LinksSlots;
  stack: StackSlots;
  bars: BarsSlots;
  flow: FlowSlots;
  levels: LevelsSlots;
  'columns-list': ColumnsListSlots;
  timeline: TimelineSlots;
  loop: LoopSlots;
  close: CloseSlots;
  join: JoinSlots;
  poll: PollSlots;
  qa: QaSlots;
};

// Every top-level slot key of each layout, at runtime, built from each layout's
// own KEYS. The `satisfies` clause and SlotKeysComplete below stop a layout
// from being wired to another layout's keys.
export const SLOT_KEYS = {
  title: TITLE_KEYS,
  divider: DIVIDER_KEYS,
  bullets: BULLETS_KEYS,
  'two-column': TWO_COLUMN_KEYS,
  practice: PRACTICE_KEYS,
  compare: COMPARE_KEYS,
  'big-number': BIG_NUMBER_KEYS,
  quote: QUOTE_KEYS,
  image: IMAGE_KEYS,
  video: VIDEO_KEYS,
  table: TABLE_KEYS,
  chart: CHART_KEYS,
  gantt: GANTT_KEYS,
  'numbered-cards': NUMBERED_CARDS_KEYS,
  rules: RULES_KEYS,
  cards: CARDS_KEYS,
  statement: STATEMENT_KEYS,
  links: LINKS_KEYS,
  stack: STACK_KEYS,
  bars: BARS_KEYS,
  flow: FLOW_KEYS,
  levels: LEVELS_KEYS,
  'columns-list': COLUMNS_LIST_KEYS,
  timeline: TIMELINE_KEYS,
  loop: LOOP_KEYS,
  close: CLOSE_KEYS,
  join: JOIN_KEYS,
  poll: POLL_KEYS,
  qa: QA_KEYS,
} as const satisfies { [L in LayoutName]: readonly (keyof Slots[L])[] };

type UnlistedSlotKey = {
  [L in LayoutName]: Exclude<keyof Slots[L], (typeof SLOT_KEYS)[L][number]>;
}[LayoutName];
// Fails to compile when a slot key of Slots is missing from SLOT_KEYS.
export type SlotKeysComplete = AssertTrue<[UnlistedSlotKey] extends [never] ? true : false>;
