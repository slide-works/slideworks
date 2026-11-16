import type { Slots } from '../slots';

// The most text the server's fit check (SlideFit, LayoutRules) accepts for the
// bullets, two-column, practice and compare layouts: each body's estimate sits
// within a few px of the 620 px the server allows. The text is ordinary
// English prose, cut to the length that fills the lines the server counts.
// (Text heavy in capitals and wide letters such as M and W runs wider than the
// server's 0.5 em estimate for Inter, so it is not used here.)

const PROSE =
  'The team ships a small slice of work to customers every week, and the ' +
  'feedback arrives in days rather than quarters, so the risk shows up early ' +
  'while it is still cheap to change course and nobody has to guess how far ' +
  'along the work really is.';

// Ordinary prose of exactly `length` characters, starting with a capital.
export function fill(length: number): string {
  let text = '';
  while (text.length < length) text += (text ? ' ' : '') + PROSE;
  return text.slice(0, length).trimEnd().padEnd(length, 'e');
}

const kicker = fill(40);
const headline = fill(80);

export const SAMPLE_IMAGE_ID = 'img_sample0001';

export const MAX_SLOTS = {
  // Eight one-line points.
  bulletsEight: {
    kicker,
    headline,
    items: Array.from({ length: 8 }, () => fill(80)),
    reveal: 'one-by-one',
  } satisfies Slots['bullets'],
  // Three two-line lead points, a one-line point and a two-line callout.
  bulletsCallout: {
    kicker,
    headline,
    items: [
      { lead: fill(50), text: fill(100) },
      { lead: fill(50), text: fill(100) },
      { lead: fill(50), text: fill(100) },
      fill(80),
    ],
    callout: { tone: 'info', text: fill(160) },
  } satisfies Slots['bullets'],
  // A two-line title over six points on the left; a title and the image on the right.
  twoColumnItems: {
    kicker,
    headline,
    left: {
      title: fill(50),
      items: [
        { lead: fill(40), text: fill(50) },
        { lead: fill(40), text: fill(50) },
        fill(90),
        fill(45),
        fill(45),
        fill(45),
      ],
    },
    right: {
      title: fill(50),
      image: { image: SAMPLE_IMAGE_ID, alt: fill(200) },
    },
  } satisfies Slots['two-column'],
  // A title and a 400-character paragraph in each column.
  twoColumnText: {
    kicker,
    headline,
    left: { title: fill(50), text: fill(400) },
    right: { title: fill(50), text: fill(400) },
  } satisfies Slots['two-column'],
  // Five Do cards (two of two lines); two of each list and a two-line callout.
  practice: {
    kicker,
    headline,
    do: [
      { lead: fill(50), text: fill(75) },
      { lead: fill(50), text: fill(75) },
      { lead: fill(25), text: fill(39) },
      { lead: fill(25), text: fill(39) },
      { lead: fill(25), text: fill(39) },
    ],
    comes_back: [fill(55), fill(55)],
    avoid: [fill(55), fill(55)],
    callout: { tone: 'bad', text: fill(80) },
  } satisfies Slots['practice'],
  // Both panels with a two-line title and sub and three two-line points.
  compare: {
    kicker,
    headline,
    failure: {
      label: fill(24),
      title: fill(50),
      sub: fill(100),
      points: [fill(90), fill(90), fill(90)],
    },
    success: {
      label: fill(24),
      title: fill(50),
      sub: fill(100),
      points: [fill(90), fill(90), fill(90)],
    },
  } satisfies Slots['compare'],
};

export const MAX_LAYOUT = {
  bulletsEight: 'bullets',
  bulletsCallout: 'bullets',
  twoColumnItems: 'two-column',
  twoColumnText: 'two-column',
  practice: 'practice',
  compare: 'compare',
} as const satisfies { [K in keyof typeof MAX_SLOTS]: keyof Slots };
