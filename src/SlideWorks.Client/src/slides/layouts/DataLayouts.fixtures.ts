import { legacyFitFixturesOf } from '../../test-support/fixtures/fitFixtures';
import type { Slots } from '../slots';

// The max-content fit fixtures for the image, table, chart and gantt layouts,
// shared with the server: src/contracts/fit-fixtures/<layout>.json holds the most text
// the validator accepts in each, in Title Case and in prose, and
// SlideWorks.UnitTest's FitFixturesTests proves each is accepted and that one
// more word is refused, by the rule `refusedBy` names: the table's fit rule
// ("fit") or a character limit ("length") for the layouts without one.
// `estimate` is LayoutRules.Table's height in px below the body's top; the
// other layouts have none.

const DATA_LAYOUTS = ['image', 'table', 'chart', 'gantt'] as const;
type DataLayout = (typeof DATA_LAYOUTS)[number];

export type DataFitFixture = {
  [L in DataLayout]: {
    layout: L;
    estimate?: number;
    grow: string[];
    refusedBy: 'fit' | 'length';
    slots: Slots[L];
  };
}[DataLayout];

export const DATA_FIT_FIXTURES = legacyFitFixturesOf(DATA_LAYOUTS) as Record<
  string,
  DataFitFixture
>;

const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';

// Wide-letter text (capitals, W and M) of exactly `length` characters.
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

// Slot keys whose strings are ids or names from a fixed set, not text.
const NOT_TEXT = new Set(['image', 'kind', 'status', 'tone']);

function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

// The same fixture with every text slot replaced by wide-letter text (capitals,
// W and M) of the same length.
export function wideTwin(fixture: DataFitFixture): DataFitFixture {
  return { ...fixture, slots: widen(fixture.slots) } as DataFitFixture;
}
