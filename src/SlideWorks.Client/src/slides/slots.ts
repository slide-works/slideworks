// The slot types for each layout (SW-1 plan section 4, contract K2). Layout and
// field names are frozen; the server's SlideValidator sets the limits. Slot text
// is plain text: never parse it as Markdown or HTML (D12).

import type { LookView } from '../api/types';

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
  'table',
  'chart',
  'gantt',
  'numbered-cards',
  'rules',
  'cards',
  'close',
  'join',
  'poll',
  'qa',
] as const;

export type LayoutName = (typeof LAYOUT_NAMES)[number];

// Title, divider and close slides use the dark surface; every other layout is light.
export const DARK_LAYOUTS: readonly LayoutName[] = ['title', 'divider', 'close', 'join'];

// A panel's tone (SW-2 L4). good, bad and info stay valid for ever as aliases of
// positive, negative and accent (D15); stored slides are never rewritten.
export type Tone = 'accent' | 'positive' | 'negative' | 'inverse' | 'good' | 'bad' | 'info';
// A toned panel's fill; soft when left out.
export type Fill = 'soft' | 'solid';
export type Reveal = 'all' | 'one-by-one';

// Inline blocks. `chip`, `badge` and `stamp` are plain strings in the slots;
// `journey` is rendered from the deck's parts and is never set by an agent.
export type CalloutBlock = { tone: Tone; fill?: Fill; text: string };

// An uploaded image: `image` is its id (served at /api/images/{id}).
export type ImageRef = { image: string; alt: string };

export type LeadText = { lead: string; text: string };
export type BulletItem = string | LeadText;
export type TitleText = { title: string; text: string };

export type TitleSlots = {
  kicker?: string;
  title: string;
  subtitle?: string;
  tagline?: string;
  author?: string;
  affiliation?: string;
  stamp?: string;
};

export type DividerSlots = { title: string; sub?: string };

export type BulletsSlots = {
  kicker?: string;
  headline: string;
  items: BulletItem[];
  reveal?: Reveal;
  callout?: CalloutBlock;
};

// Exactly one of items, text or image is set.
export type ColumnSlot = {
  title?: string;
  items?: BulletItem[];
  text?: string;
  image?: ImageRef;
};

export type TwoColumnSlots = {
  kicker?: string;
  headline: string;
  left: ColumnSlot;
  right: ColumnSlot;
};

export type PracticeSlots = {
  kicker?: string;
  headline: string;
  do: LeadText[];
  comes_back: string[];
  avoid: string[];
  callout?: CalloutBlock;
};

// failure defaults to the negative tone and success to the positive one.
export type CompareSide = {
  label: string;
  title: string;
  sub?: string;
  points: string[];
  tone?: Tone;
  fill?: Fill;
};

export type CompareSlots = {
  kicker?: string;
  headline: string;
  failure: CompareSide;
  success: CompareSide;
};

export type BigNumberSlots = {
  kicker?: string;
  headline: string;
  value: string;
  caption: string;
  detail?: string;
  source?: string;
};

export type QuoteSlots = { kicker?: string; quote: string; attribution: string };

export type ImageSlots = {
  kicker?: string;
  headline: string;
  image: ImageRef;
  points?: string[];
  caption?: string;
};

// A toned cell is drawn as a badge; table cells also have a warning tone.
export type CellTone = 'good' | 'warn' | 'bad' | 'info';
export type TableCell = string | { text: string; tone: CellTone };

export type TableSlots = {
  kicker?: string;
  headline: string;
  columns: string[];
  rows: TableCell[][];
  // The 1-based number of the row to highlight.
  highlightRow?: number;
};

export type ChartSeries = { name: string; values: number[] };

export type ChartSlots = {
  kicker?: string;
  headline: string;
  kind: 'line' | 'bar';
  xLabels: string[];
  series: ChartSeries[];
  yUnit?: string;
  caption?: string;
  // x is 1-based on xLabels: 1 is the first label, 3.5 sits between the third and fourth.
  marker?: { x: number; label: string };
};

export type GanttStatus = 'done' | 'slips' | 'stopped' | 'ship';

// start and end are 1-based column numbers, end inclusive.
export type GanttRow = {
  label: string;
  start: number;
  end: number;
  status: GanttStatus;
  note?: string;
};

export type GanttSlots = {
  kicker?: string;
  headline: string;
  columns: string[];
  rows: GanttRow[];
  caption?: string;
};

export type NumberedCardsSlots = {
  kicker?: string;
  headline: string;
  cards: TitleText[];
  reveal?: Reveal;
};

export type RulesSlots = { kicker?: string; headline: string; rules: TitleText[]; reveal?: Reveal };

// A card with no tone is the plain card.
export type Card = {
  badge?: string;
  title: string;
  text: string;
  chips: string[];
  tone?: Tone;
  fill?: Fill;
};

export type CardsSlots = { kicker?: string; headline: string; cards: Card[]; reveal?: Reveal };

// headline may carry \n line breaks; it is the only slot that does.
export type CloseSlots = { headline: string; sub?: string; author?: string };

// The join slide's slots (SW-5 J6). The code and the QR come from the live session, never from the slots.
export type JoinSlots = { kicker?: string; count?: 'shown' | 'hidden' };

// The poll slide's slots (SW-6 V1): the question and 2 to 8 options, known by position. The poll's settings (pick,
// results, correct, opens) are never slots: they live in the poll's own row, so the correct answer never reaches a viewer.
export type PollSlots = { question: string; options: string[] };

// The Q&A slide's one slot (SW-9 Q1): a title of up to 80 characters; left out, the slide draws "Questions from the room".
// The questions come from the live session, never from the slots.
export type QaSlots = { title?: string };

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
  table: TableSlots;
  chart: ChartSlots;
  gantt: GanttSlots;
  'numbered-cards': NumberedCardsSlots;
  rules: RulesSlots;
  cards: CardsSlots;
  close: CloseSlots;
  join: JoinSlots;
  poll: PollSlots;
  qa: QaSlots;
};

// Every top-level slot key of each layout, at runtime. The `satisfies` clause
// stops a key that isn't in the type, and SlotKeysComplete below stops a key
// of the type from being left out, so the table can't drift from Slots.
export const SLOT_KEYS = {
  title: ['kicker', 'title', 'subtitle', 'tagline', 'author', 'affiliation', 'stamp'],
  divider: ['title', 'sub'],
  bullets: ['kicker', 'headline', 'items', 'reveal', 'callout'],
  'two-column': ['kicker', 'headline', 'left', 'right'],
  practice: ['kicker', 'headline', 'do', 'comes_back', 'avoid', 'callout'],
  compare: ['kicker', 'headline', 'failure', 'success'],
  'big-number': ['kicker', 'headline', 'value', 'caption', 'detail', 'source'],
  quote: ['kicker', 'quote', 'attribution'],
  image: ['kicker', 'headline', 'image', 'points', 'caption'],
  table: ['kicker', 'headline', 'columns', 'rows', 'highlightRow'],
  chart: ['kicker', 'headline', 'kind', 'xLabels', 'series', 'yUnit', 'caption', 'marker'],
  gantt: ['kicker', 'headline', 'columns', 'rows', 'caption'],
  'numbered-cards': ['kicker', 'headline', 'cards', 'reveal'],
  rules: ['kicker', 'headline', 'rules', 'reveal'],
  cards: ['kicker', 'headline', 'cards', 'reveal'],
  close: ['headline', 'sub', 'author'],
  join: ['kicker', 'count'],
  poll: ['question', 'options'],
  qa: ['title'],
} as const satisfies { [L in LayoutName]: readonly (keyof Slots[L])[] };

type UnlistedSlotKey = {
  [L in LayoutName]: Exclude<keyof Slots[L], (typeof SLOT_KEYS)[L][number]>;
}[LayoutName];
type AssertTrue<T extends true> = T;
// Fails to compile when a slot key of Slots is missing from SLOT_KEYS.
export type SlotKeysComplete = AssertTrue<[UnlistedSlotKey] extends [never] ? true : false>;

// A slide flagged deep_dive, as the divider lists it: its part (1-based, or
// null), its slide number and its headline.
export type DeepDive = { part: number | null; number: number; headline: string };

// What a layout knows about its deck. The player passes the play view's values;
// the deck page's thumbnails pass the deck page's (with a null author). look is
// the deck's resolved look, and deepDives comes from deepDivesOf(slides).
export type SlideDeck = {
  title: string;
  author: string | null;
  parts: string[];
  look: LookView;
  deepDives: DeepDive[];
};

// The props every layout component takes (K12). `step` is 0 before any reveal.
export type LayoutProps<L extends LayoutName> = { slots: Slots[L]; step: number; deck: SlideDeck };
