import type { Bar } from '../slots/bars';
import { glyphCost } from './glyphWidths';

// The bars layout's geometry, in canvas px. It is the one source: the server's
// BarsLayout rule (src/SlideWorks.Core/Slides/Layouts/BarsLayout.cs) mirrors
// these numbers one for one, so a change here is a change there.
//
// Grid:     400 px label column, 32 px gap, 1248 px track.
// Bars:     96 px high for 2–3 bars, 80 for 4, 64 for 5, 56 for 6.
// Rows:     28 px apart, 20 px for 5 or 6 bars.
// Legend:   a 38 px row above the bars.
// Caption:  34 px under the last bar, 24 px text on 32.4 px lines.
// Label:    30 px on 36 px lines; the sub 20 px on 26 px lines, 4 px under it.
// End label: the value (and unit) at 28 px × 0.62 em per character, the tag at
//           18 px × 0.85 em per character + 14, and 30 px.
// Every "per character" is the sum of the characters' glyphWidths factors (a W costs 1.87, a lowercase
// letter 1), the server's GlyphWidths table.
// A bar is at least 6 px long. A note at 22 px × 0.56 em per character is
// inside its bar when it and 40 px fit, else 18 px after the bar or outline.
export const LABEL_COLUMN = 400;
export const COLUMN_GAP = 32;
export const TRACK = 1248;
export const LEGEND = 38;
export const CAPTION_GAP = 34;
export const CAPTION_FONT = 24;
export const CAPTION_LINE = 32.4;
export const LABEL_FONT = 30;
export const LABEL_LINE = 36;
export const SUB_FONT = 20;
export const SUB_LINE = 26;
export const SUB_GAP = 4;
export const VALUE_FONT = 28;
export const VALUE_EM = 0.62;
export const TAG_FONT = 18;
export const TAG_EM = 0.85;
export const TAG_PAD = 14;
export const END_PAD = 30;
export const MIN_BAR = 6;
export const NOTE_FONT = 22;
export const NOTE_EM = 0.56;
export const NOTE_PAD = 40;
export const NOTE_GAP = 18;

export function barHeight(count: number): number {
  if (count <= 3) return 96;
  if (count === 4) return 80;
  return count === 5 ? 64 : 56;
}

export function rowGap(count: number): number {
  return count >= 5 ? 20 : 28;
}

// Text width in average-character units: each character costs its glyphWidths factor, as on the server.
const length = glyphCost;

const NUMBER = new Intl.NumberFormat('en', { maximumFractionDigits: 15 });

// A value as it is drawn, grouped in thousands.
export function formatValue(value: number): string {
  return NUMBER.format(value);
}

// The value and its unit as the end label draws them; empty when the numbers
// are hidden.
export function valueText(value: number, unit: string | undefined, shown: boolean): string {
  if (!shown) return '';
  return unit === undefined ? formatValue(value) : `${formatValue(value)} ${unit}`;
}

// The room a bar's end label needs: the value and unit, the tag, and 30 px;
// 0 when it has neither.
export function endWidth(bar: Bar, unit: string | undefined, shown: boolean): number {
  let width = length(valueText(bar.value, unit, shown)) * VALUE_FONT * VALUE_EM;
  if (bar.tag !== undefined) width += length(bar.tag) * TAG_FONT * TAG_EM + TAG_PAD;
  return width > 0 ? width + END_PAD : 0;
}

// The width of a note, without its padding.
export function noteWidth(note: string): number {
  return length(note) * NOTE_FONT * NOTE_EM;
}

export type Placement = {
  // The bar's length, from the left of the track.
  length: number;
  // The dashed outline's length, when the bar has a was.
  outline: number | null;
  // Where the end label (and a note placed after) starts, from the left.
  endLeft: number;
  // Where the note is written, when the bar has one.
  note: 'inside' | 'after' | null;
};

export type Plan = {
  // The track's width left for the bars: the track less the widest end label.
  available: number;
  placements: Placement[];
};

// The scale (0 to the largest value or was), the room kept for the widest end
// label, and where each bar's outline, note and end label go.
export function planBars(bars: Bar[], unit: string | undefined, shown: boolean): Plan {
  const largest = Math.max(...bars.map((b) => Math.max(b.value, b.was ?? 0)));
  const available = TRACK - Math.max(...bars.map((b) => endWidth(b, unit, shown)));
  const scale = (value: number) => Math.max(MIN_BAR, (value / largest) * available);
  const placements = bars.map((bar): Placement => {
    const own = scale(bar.value);
    const outline = bar.was === undefined ? null : scale(bar.was);
    const end = Math.max(own, outline ?? 0);
    let note: Placement['note'] = null;
    if (bar.note !== undefined) {
      note = noteWidth(bar.note) + NOTE_PAD <= own ? 'inside' : 'after';
    }
    return { length: own, outline, endLeft: end + NOTE_GAP, note };
  });
  return { available, placements };
}
