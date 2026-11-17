import type { LayoutName, Slots } from '../../slides/slots';
import type { AssertTrue, RequiredKey } from '../../slides/slots/keyChecks';
import { REQUIRED as TITLE_REQUIRED, SLOTS as TITLE_SLOTS } from './layoutSlots/title';
import { REQUIRED as DIVIDER_REQUIRED, SLOTS as DIVIDER_SLOTS } from './layoutSlots/divider';
import { REQUIRED as BULLETS_REQUIRED, SLOTS as BULLETS_SLOTS } from './layoutSlots/bullets';
import {
  REQUIRED as TWO_COLUMN_REQUIRED,
  SLOTS as TWO_COLUMN_SLOTS,
} from './layoutSlots/two-column';
import { REQUIRED as PRACTICE_REQUIRED, SLOTS as PRACTICE_SLOTS } from './layoutSlots/practice';
import { REQUIRED as COMPARE_REQUIRED, SLOTS as COMPARE_SLOTS } from './layoutSlots/compare';
import {
  REQUIRED as BIG_NUMBER_REQUIRED,
  SLOTS as BIG_NUMBER_SLOTS,
} from './layoutSlots/big-number';
import { REQUIRED as QUOTE_REQUIRED, SLOTS as QUOTE_SLOTS } from './layoutSlots/quote';
import { REQUIRED as IMAGE_REQUIRED, SLOTS as IMAGE_SLOTS } from './layoutSlots/image';
import { REQUIRED as TABLE_REQUIRED, SLOTS as TABLE_SLOTS } from './layoutSlots/table';
import { REQUIRED as CHART_REQUIRED, SLOTS as CHART_SLOTS } from './layoutSlots/chart';
import { REQUIRED as GANTT_REQUIRED, SLOTS as GANTT_SLOTS } from './layoutSlots/gantt';
import {
  REQUIRED as NUMBERED_CARDS_REQUIRED,
  SLOTS as NUMBERED_CARDS_SLOTS,
} from './layoutSlots/numbered-cards';
import { REQUIRED as RULES_REQUIRED, SLOTS as RULES_SLOTS } from './layoutSlots/rules';
import { REQUIRED as CARDS_REQUIRED, SLOTS as CARDS_SLOTS } from './layoutSlots/cards';
import { REQUIRED as STATEMENT_REQUIRED, SLOTS as STATEMENT_SLOTS } from './layoutSlots/statement';
import { REQUIRED as LINKS_REQUIRED, SLOTS as LINKS_SLOTS } from './layoutSlots/links';
import { REQUIRED as STACK_REQUIRED, SLOTS as STACK_SLOTS } from './layoutSlots/stack';
import { REQUIRED as BARS_REQUIRED, SLOTS as BARS_SLOTS } from './layoutSlots/bars';
import { REQUIRED as FLOW_REQUIRED, SLOTS as FLOW_SLOTS } from './layoutSlots/flow';
import { REQUIRED as LEVELS_REQUIRED, SLOTS as LEVELS_SLOTS } from './layoutSlots/levels';
import { REQUIRED as CLOSE_REQUIRED, SLOTS as CLOSE_SLOTS } from './layoutSlots/close';
import { REQUIRED as JOIN_REQUIRED, SLOTS as JOIN_SLOTS } from './layoutSlots/join';
import { REQUIRED as POLL_REQUIRED, SLOTS as POLL_SLOTS } from './layoutSlots/poll';
import { REQUIRED as QA_REQUIRED, SLOTS as QA_SLOTS } from './layoutSlots/qa';

export { SAMPLE_IMAGE_ID } from './layoutSlots/sampleImage';

// One valid slide's slots for every layout, with every optional slot set. Each
// layout's fixture and its required keys live in layoutSlots/<layout>.ts (K5).
export const LAYOUT_SLOTS: { [L in keyof Slots]: Slots[L] } = {
  title: TITLE_SLOTS,
  divider: DIVIDER_SLOTS,
  bullets: BULLETS_SLOTS,
  'two-column': TWO_COLUMN_SLOTS,
  practice: PRACTICE_SLOTS,
  compare: COMPARE_SLOTS,
  'big-number': BIG_NUMBER_SLOTS,
  quote: QUOTE_SLOTS,
  image: IMAGE_SLOTS,
  table: TABLE_SLOTS,
  chart: CHART_SLOTS,
  gantt: GANTT_SLOTS,
  'numbered-cards': NUMBERED_CARDS_SLOTS,
  rules: RULES_SLOTS,
  cards: CARDS_SLOTS,
  statement: STATEMENT_SLOTS,
  links: LINKS_SLOTS,
  stack: STACK_SLOTS,
  bars: BARS_SLOTS,
  flow: FLOW_SLOTS,
  levels: LEVELS_SLOTS,
  close: CLOSE_SLOTS,
  join: JOIN_SLOTS,
  poll: POLL_SLOTS,
  qa: QA_SLOTS,
};

// The slot keys a layout can't do without, built from each layout's own
// REQUIRED. The `satisfies` clause and RequiredKeysComplete stop a layout from
// being wired to another layout's keys.
export const REQUIRED_SLOT_KEYS = {
  title: TITLE_REQUIRED,
  divider: DIVIDER_REQUIRED,
  bullets: BULLETS_REQUIRED,
  'two-column': TWO_COLUMN_REQUIRED,
  practice: PRACTICE_REQUIRED,
  compare: COMPARE_REQUIRED,
  'big-number': BIG_NUMBER_REQUIRED,
  quote: QUOTE_REQUIRED,
  image: IMAGE_REQUIRED,
  table: TABLE_REQUIRED,
  chart: CHART_REQUIRED,
  gantt: GANTT_REQUIRED,
  'numbered-cards': NUMBERED_CARDS_REQUIRED,
  rules: RULES_REQUIRED,
  cards: CARDS_REQUIRED,
  statement: STATEMENT_REQUIRED,
  links: LINKS_REQUIRED,
  stack: STACK_REQUIRED,
  bars: BARS_REQUIRED,
  flow: FLOW_REQUIRED,
  levels: LEVELS_REQUIRED,
  close: CLOSE_REQUIRED,
  join: JOIN_REQUIRED,
  poll: POLL_REQUIRED,
  qa: QA_REQUIRED,
} as const satisfies { [L in LayoutName]: readonly RequiredKey<Slots[L]>[] };

type UnlistedRequiredKey = {
  [L in LayoutName]: Exclude<RequiredKey<Slots[L]>, (typeof REQUIRED_SLOT_KEYS)[L][number]>;
}[LayoutName];
// Fails to compile when a required slot key is missing from REQUIRED_SLOT_KEYS.
export type RequiredKeysComplete = AssertTrue<[UnlistedRequiredKey] extends [never] ? true : false>;
