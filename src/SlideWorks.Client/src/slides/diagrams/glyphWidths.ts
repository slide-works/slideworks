// How many "average characters" one character costs: the client port of the server's
// src/SlideWorks.Core/Slides/GlyphWidths.cs, factor for factor (glyphWidths.test.ts fails when the two
// tables differ). No factor is below 1.0. A bar's labels never wrap, so their width is the sum of the
// characters' factors times the font size times the em.
const WIDEST = 1.87;
const AT_SIGN = 1.82;
const WIDE = 1.67;
const WIDE_ROUND = 1.58;
const CAPITAL_G = 1.46;
const CAPITAL_BROAD = 1.39;
const CAPITAL = 1.24;
const ZERO = 1.28;
const DIGIT = 1.22;

export function glyphFactor(character: string): number {
  if ('%W'.includes(character)) return WIDEST;
  if (character === '@') return AT_SIGN;
  if ('Mm#'.includes(character)) return WIDE;
  if ('wOQ'.includes(character)) return WIDE_ROUND;
  if ('G&'.includes(character)) return CAPITAL_G;
  if ('CNHAVDXUYK'.includes(character)) return CAPITAL_BROAD;
  if (character >= 'A' && character <= 'Z') return CAPITAL;
  if (character === '0') return ZERO;
  if ((character >= '1' && character <= '9') || '+<=>~$^_'.includes(character)) return DIGIT;
  return 1;
}

// What a text costs: the sum of its characters' factors. A letter with its combining marks is one
// character, as on the server; the factor is the first code point's. Without Intl.Segmenter the
// count is in code points.
export function glyphCost(text: string): number {
  let total = 0;
  for (const element of elements(text)) total += glyphFactor(element[0]);
  return total;
}

function elements(text: string): string[] {
  try {
    return Array.from(new Intl.Segmenter(undefined, { granularity: 'grapheme' }).segment(text), (s) => s.segment);
  } catch {
    return Array.from(text);
  }
}
