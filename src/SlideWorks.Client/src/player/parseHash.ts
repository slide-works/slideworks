// Where the player is: a slide (1-based) and how many of its steps are shown.
export type PlayerPosition = { slide: number; step: number };

export const FIRST_POSITION: PlayerPosition = { slide: 1, step: 0 };

// Reads a `#n` or `#n.s` deep link against the deck's step counts (one entry
// per slide). Anything that names no slide of this deck is null, so the caller
// falls back to slide 1. A step past the slide's last one is clamped to it, so
// a link survives a slide losing an item.
export function parseHash(hash: string, stepCounts: readonly number[]): PlayerPosition | null {
  const match = /^#?([1-9]\d{0,5})(?:\.(\d{1,6}))?$/.exec(hash);
  if (!match) return null;
  const slide = Number(match[1]);
  if (slide > stepCounts.length) return null;
  const step = match[2] === undefined ? 0 : Number(match[2]);
  return { slide, step: Math.min(step, stepCounts[slide - 1]) };
}

// The hash for a position: `#n` before any step, `#n.s` after.
export function formatHash({ slide, step }: PlayerPosition): string {
  return step > 0 ? `#${slide}.${step}` : `#${slide}`;
}
