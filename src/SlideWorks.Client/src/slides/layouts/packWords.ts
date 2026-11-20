import CloudLayout, { type CloudSprite, type SpriteOptions } from 'd3-cloud';
import type { CloudWord } from '../../openResponses/types';

// Canvas px: the word with the room's highest count starts at LARGEST_PX and a word nobody else sent at SMALLEST_PX. A
// word that doesn't fit steps down, never below FLOOR_PX, and no word is drawn larger than one with a higher count.
const LARGEST_PX = 168;
const SMALLEST_PX = 48;
const FLOOR_PX = 24;
const STEP = 0.85;
const PADDING = 4;
export const WORD_WEIGHT = 800;

// The palette's inks that read on the light slide. A word's ink comes from its text, so it keeps it as the cloud grows.
const INKS = [
  'var(--sw-accent)',
  'var(--sw-ink)',
  'var(--sw-dd)',
  'var(--sw-accent-dark)',
  'var(--sw-panel-positive)',
];

export type PackedWord = {
  text: string;
  // The anchor, the middle of the word on its baseline, from the box's centre.
  x: number;
  y: number;
  size: number;
  rotate: number;
  ink: string;
};

export type Spot = { x: number; y: number };

// FNV-1a: the same text gives the same number on every screen and every reconnect.
function hashOf(text: string): number {
  let hash = 0x811c9dc5;
  for (const char of text.toLowerCase()) {
    hash ^= char.codePointAt(0) ?? 0;
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

// Mulberry32, seeded from the word, so where a word starts its search depends on the word and not on the words before it.
function randomOf(seed: number): () => number {
  let state = seed;
  return () => {
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const inkOf = (text: string) => INKS[hashOf(text) % INKS.length];

// About one word in three turns on its side, picked from the text, never at random.
const turnsOf = (text: string) => (hashOf(text) >>> 8) % 3 === 0;

// Sprites are rasterised once per text, size, angle and font, and placed in any later pack.
export type SpriteCache = Map<string, CloudSprite<CloudWord> | null>;

// Packs the words, highest count first as the room's tally sends them, inside a width × height box centred on 0, 0. A
// new word starts its search at the centre and a word already drawn at its previous spot, so a new word joins without
// moving the words already placed while there is room. A word that doesn't fit gets smaller; one that doesn't fit at
// FLOOR_PX is left out, with every word after it.
export function packWords(
  words: readonly CloudWord[],
  box: { width: number; height: number },
  face: Pick<SpriteOptions, 'font'>,
  turn: boolean,
  previous: ReadonlyMap<string, Spot>,
  sprites: SpriteCache,
): PackedWord[] {
  const layout = new CloudLayout().size([box.width, box.height]).overflow(false);
  const highest = Math.max(1, ...words.map((word) => word.count));
  const packed: PackedWord[] = [];
  let cap = LARGEST_PX;
  for (const word of words) {
    const angles = turn && turnsOf(word.text) ? [-90, 0] : [0];
    const start = Math.round(SMALLEST_PX + ((LARGEST_PX - SMALLEST_PX) * word.count) / highest);
    const seed = previous.get(word.text) ?? { x: 0, y: 0 };
    const placed = place(layout, word, Math.min(cap, start), angles, face, seed, sprites);
    if (placed === null) break;
    cap = placed.size;
    packed.push({ ...placed, text: word.text, ink: inkOf(word.text) });
  }
  return packed;
}

// Places the word at the largest size from `start` down to FLOOR_PX at which it fits, at the first of the angles that
// fits at that size, so a turned word too long for the body's height lies level before it shrinks. Null when none does.
function place(
  layout: CloudLayout,
  word: CloudWord,
  start: number,
  angles: readonly number[],
  face: Pick<SpriteOptions, 'font'>,
  seed: Spot,
  sprites: SpriteCache,
): (Spot & { size: number; rotate: number }) | null {
  for (let size = start; ; size = Math.max(FLOOR_PX, Math.floor(size * STEP))) {
    for (const rotate of angles) {
      const key = `${word.text}|${size}|${rotate}|${face.font}`;
      if (!sprites.has(key)) {
        const options = {
          ...face,
          weight: WORD_WEIGHT,
          size,
          rotate,
          padding: PADDING,
          datum: word,
        };
        sprites.set(key, layout.getSprite(word.text, options));
      }
      const sprite = sprites.get(key);
      layout.random(randomOf(hashOf(word.text)));
      const spot = sprite ? layout.place(sprite, seed) : null;
      if (spot !== null) return { x: spot.x, y: spot.y, size, rotate };
    }
    if (size <= FLOOR_PX) return null;
  }
}
