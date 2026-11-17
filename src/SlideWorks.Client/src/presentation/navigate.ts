// The presenter's navigation rules (contract P5), pure. Every window and phone
// works out its next and previous position here and sends it with `update`;
// the server receives positions only. Positions keep the deck's numbers: slide
// is 1-based and step 0-based, as the player's #slide.step (K12), and
// stepCounts[n - 1] is slide n's number of steps, so step runs 0..stepCounts[n - 1].
//
// With skip on, next and previous step over every deep-dive slide whole, reveals
// included, and nothing is renumbered. A jump (overview, slide sheet, number +
// Enter) goes to {slide: n, step: 0} even when n is a deep dive, and next and
// previous then walk that slide's steps as usual before leaving it.

export type Position = { slide: number; step: number };

export type NextPreview =
  | { kind: 'step'; slide: number; step: number }
  | { kind: 'slide'; slide: number; skipped: number[] }
  | { kind: 'end' };

function skipped(slide: number, deepDives: ReadonlySet<number>, skip: boolean): boolean {
  return skip && deepDives.has(slide);
}

// The nearest slide after `from` that isn't skipped, with the deep dives passed
// on the way, or null when there is none.
function slideAfter(
  from: number,
  total: number,
  deepDives: ReadonlySet<number>,
  skip: boolean,
): { slide: number; skipped: number[] } | null {
  const passed: number[] = [];
  for (let slide = from + 1; slide <= total; slide++) {
    if (!skipped(slide, deepDives, skip)) return { slide, skipped: passed };
    passed.push(slide);
  }
  return null;
}

function slideBefore(from: number, deepDives: ReadonlySet<number>, skip: boolean): number | null {
  for (let slide = from - 1; slide >= 1; slide--) {
    if (!skipped(slide, deepDives, skip)) return slide;
  }
  return null;
}

// What next would show: the slide's next step, the next slide (naming the deep
// dives it steps over), or the end of the deck.
export function nextPreview(
  pos: Position,
  stepCounts: readonly number[],
  deepDives: ReadonlySet<number>,
  skip: boolean,
): NextPreview {
  if (pos.step < (stepCounts[pos.slide - 1] ?? 0)) {
    return { kind: 'step', slide: pos.slide, step: pos.step + 1 };
  }
  const after = slideAfter(pos.slide, stepCounts.length, deepDives, skip);
  return after ? { kind: 'slide', ...after } : { kind: 'end' };
}

// One step on, or the next slide at step 0. The same object at the end.
export function nextPosition(
  pos: Position,
  stepCounts: readonly number[],
  deepDives: ReadonlySet<number>,
  skip: boolean,
): Position {
  const preview = nextPreview(pos, stepCounts, deepDives, skip);
  switch (preview.kind) {
    case 'step':
      return { slide: preview.slide, step: preview.step };
    case 'slide':
      return { slide: preview.slide, step: 0 };
    case 'end':
      return pos;
  }
}

// One step back, or the earlier slide with all its steps shown (K12). The same
// object at the start.
export function previousPosition(
  pos: Position,
  stepCounts: readonly number[],
  deepDives: ReadonlySet<number>,
  skip: boolean,
): Position {
  if (pos.step > 0) return { slide: pos.slide, step: pos.step - 1 };
  const before = slideBefore(pos.slide, deepDives, skip);
  return before === null ? pos : { slide: before, step: stepCounts[before - 1] ?? 0 };
}

// How many deep dives come after the current slide ("2 deep dives ahead").
export function deepDivesAhead(pos: Position, deepDives: ReadonlySet<number>): number {
  let count = 0;
  for (const slide of deepDives) if (slide > pos.slide) count++;
  return count;
}
