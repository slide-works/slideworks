// The scale a card list draws at (K14). The rule is on character counts only,
// so the server (CardScale) and the client agree and nothing is measured;
// src/contracts/card-scale-fixtures.json holds the cases for both.

// What the rule reads of a card.
export type ScaleCard = { title: string; text: string; chips?: readonly string[] };

const LARGE_TITLE = 32;
const LARGE_TEXT = 70;

// Made on first use, not at import: a browser without Intl.Segmenter must
// still load every layout that imports this module. null when there is none.
let graphemes: Intl.Segmenter | null | undefined;

function segmenter(): Intl.Segmenter | null {
  if (graphemes === undefined) {
    try {
      graphemes = new Intl.Segmenter(undefined, { granularity: 'grapheme' });
    } catch {
      graphemes = null;
    }
  }
  return graphemes;
}

// Characters as the server counts them: a letter with its combining marks is
// one. Without a segmenter the count is in code points, the nearest there is.
function length(text: string): number {
  const segments = segmenter();
  if (segments === null) return Array.from(text).length;
  let count = 0;
  for (const segment of segments.segment(text)) {
    void segment;
    count++;
  }
  return count;
}

// Large when every card has a title of at most 32 characters, a text of at
// most 70 and no chips; an empty chips list counts as no chips.
export function isLarge(cards: readonly ScaleCard[]): boolean {
  return cards.every(
    (card) =>
      length(card.title) <= LARGE_TITLE &&
      length(card.text) <= LARGE_TEXT &&
      (card.chips === undefined || card.chips.length === 0),
  );
}

// The two attributes a card list carries; its cards' CSS keys off them.
export function cardListAttrs(cards: readonly ScaleCard[]): {
  'data-count': number;
  'data-scale': 'large' | 'regular';
} {
  return { 'data-count': cards.length, 'data-scale': isLarge(cards) ? 'large' : 'regular' };
}
