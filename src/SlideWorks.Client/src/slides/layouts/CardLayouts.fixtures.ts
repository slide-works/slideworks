import type { Slots } from '../slots';
import { fill } from './ContentLayouts.fixtures';

// The most text the slot limits (src/contracts/layouts.md) accept for the
// numbered-cards, rules and cards layouts, at their maximum counts. The server
// has no fit estimate for these layouts beyond the limits, so every slot is
// filled to its limit. PROSE is ordinary English prose (fill). TITLE is the
// same prose in Title Case, and WIDE is text heavy in capitals and wide
// letters such as M and W: Inter draws both wider than the server's 0.5 em
// estimate (SW-117), so their numbers are reported but not held to y = 996.

const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';

// Ordinary prose in Title Case, of exactly `length` characters.
export function titleCase(length: number): string {
  return fill(length).replace(/\b[a-z]/g, (letter) => letter.toUpperCase());
}

// Wide-letter text of exactly `length` characters.
export function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

function maxSlots(text: (length: number) => string) {
  return {
    numberedCards: {
      kicker: text(40),
      headline: text(80),
      cards: Array.from({ length: 4 }, () => ({ title: text(40), text: text(140) })),
      reveal: 'one-by-one',
    } satisfies Slots['numbered-cards'],
    rules: {
      kicker: text(40),
      headline: text(80),
      rules: Array.from({ length: 4 }, () => ({ title: text(60), text: text(110) })),
      reveal: 'one-by-one',
    } satisfies Slots['rules'],
    cards: {
      kicker: text(40),
      headline: text(80),
      cards: Array.from({ length: 3 }, () => ({
        badge: text(20),
        title: text(50),
        text: text(140),
        chips: Array.from({ length: 4 }, () => text(24)),
      })),
      reveal: 'one-by-one',
    } satisfies Slots['cards'],
  };
}

export const MAX_PROSE = maxSlots(fill);
export const MAX_TITLE = maxSlots(titleCase);
export const MAX_WIDE = maxSlots(wide);

export const MAX_LAYOUT = {
  numberedCards: 'numbered-cards',
  rules: 'rules',
  cards: 'cards',
} as const satisfies { [K in keyof typeof MAX_PROSE]: keyof Slots };
