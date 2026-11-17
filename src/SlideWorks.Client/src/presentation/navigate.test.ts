import { describe, expect, it } from 'vitest';
import { deepDivesAhead, nextPosition, nextPreview, previousPosition } from './navigate';

// Steps per slide, by deck number: slide 3 has 2 reveals, slide 5 (a deep dive)
// has 3, the rest none. Deep dives are 5, 9 and 10 (two adjacent).
const STEPS = [0, 0, 2, 0, 3, 0, 0, 0, 1, 0, 0];
const DEEP = new Set([5, 9, 10]);
const NONE = new Set<number>();

describe('nextPosition', () => {
  it('reveals the next step, then moves to the next slide at step 0', () => {
    expect(nextPosition({ slide: 3, step: 0 }, STEPS, NONE, false)).toEqual({ slide: 3, step: 1 });
    expect(nextPosition({ slide: 3, step: 2 }, STEPS, NONE, false)).toEqual({ slide: 4, step: 0 });
  });

  it('with skip on, steps over one deep dive whole, reveals included', () => {
    expect(nextPosition({ slide: 4, step: 0 }, STEPS, DEEP, true)).toEqual({ slide: 6, step: 0 });
  });

  it('with skip on, steps over two adjacent deep dives', () => {
    expect(nextPosition({ slide: 8, step: 0 }, STEPS, DEEP, true)).toEqual({ slide: 11, step: 0 });
  });

  it('with skip off, walks every slide, deep dives and their reveals included', () => {
    const walked = [];
    let pos = { slide: 1, step: 0 };
    for (;;) {
      walked.push(`${pos.slide}.${pos.step}`);
      const next = nextPosition(pos, STEPS, DEEP, false);
      if (next === pos) break;
      pos = next;
    }
    expect(walked).toEqual([
      '1.0',
      '2.0',
      '3.0',
      '3.1',
      '3.2',
      '4.0',
      '5.0',
      '5.1',
      '5.2',
      '5.3',
      '6.0',
      '7.0',
      '8.0',
      '9.0',
      '9.1',
      '10.0',
      '11.0',
    ]);
  });

  it('stays put at the end, returning the same position', () => {
    const end = { slide: 11, step: 0 };
    expect(nextPosition(end, STEPS, DEEP, true)).toBe(end);
    expect(nextPosition(end, STEPS, NONE, false)).toBe(end);
  });

  it('with skip on, stays put when only deep dives are left', () => {
    const steps = [0, 0, 0];
    const last = { slide: 2, step: 0 };
    expect(nextPosition(last, steps, new Set([3]), true)).toBe(last);
  });

  it('on a deep dive reached by a jump, reveals its steps and then leaves it', () => {
    // A jump lands on {slide: n, step: 0} even when n is a deep dive.
    expect(nextPosition({ slide: 5, step: 0 }, STEPS, DEEP, true)).toEqual({ slide: 5, step: 1 });
    expect(nextPosition({ slide: 5, step: 3 }, STEPS, DEEP, true)).toEqual({ slide: 6, step: 0 });
  });

  it('keeps the deck numbers: nothing renumbers while skipping', () => {
    const visited = [];
    let pos = { slide: 4, step: 0 };
    for (let i = 0; i < 4; i++) {
      pos = nextPosition(pos, STEPS, DEEP, true);
      visited.push(pos.slide);
    }
    expect(visited).toEqual([6, 7, 8, 11]);
  });
});

describe('previousPosition', () => {
  it('hides the last step, then lands on the earlier slide with all its steps shown', () => {
    expect(previousPosition({ slide: 3, step: 2 }, STEPS, NONE, false)).toEqual({
      slide: 3,
      step: 1,
    });
    expect(previousPosition({ slide: 4, step: 0 }, STEPS, NONE, false)).toEqual({
      slide: 3,
      step: 2,
    });
  });

  it('with skip off, lands on a deep dive with all its steps shown', () => {
    expect(previousPosition({ slide: 6, step: 0 }, STEPS, DEEP, false)).toEqual({
      slide: 5,
      step: 3,
    });
  });

  it('with skip on, steps back over one deep dive onto the slide before, all steps shown', () => {
    expect(previousPosition({ slide: 6, step: 0 }, STEPS, DEEP, true)).toEqual({
      slide: 4,
      step: 0,
    });
    expect(previousPosition({ slide: 4, step: 0 }, STEPS, DEEP, true)).toEqual({
      slide: 3,
      step: 2,
    });
  });

  it('with skip on, steps back over two adjacent deep dives', () => {
    expect(previousPosition({ slide: 11, step: 0 }, STEPS, DEEP, true)).toEqual({
      slide: 8,
      step: 0,
    });
  });

  it('stays put at the start, returning the same position', () => {
    const start = { slide: 1, step: 0 };
    expect(previousPosition(start, STEPS, DEEP, true)).toBe(start);
    expect(previousPosition(start, STEPS, NONE, false)).toBe(start);
  });

  it('with skip on, stays put when only deep dives are before it', () => {
    const steps = [0, 0, 0];
    const second = { slide: 2, step: 0 };
    expect(previousPosition(second, steps, new Set([1]), true)).toBe(second);
  });
});

describe('nextPreview', () => {
  it('names the next step while the slide has steps left', () => {
    expect(nextPreview({ slide: 3, step: 1 }, STEPS, DEEP, true)).toEqual({
      kind: 'step',
      slide: 3,
      step: 2,
    });
  });

  it('names the next slide and the deep dives it skips', () => {
    expect(nextPreview({ slide: 4, step: 0 }, STEPS, DEEP, true)).toEqual({
      kind: 'slide',
      slide: 6,
      skipped: [5],
    });
    expect(nextPreview({ slide: 8, step: 0 }, STEPS, DEEP, true)).toEqual({
      kind: 'slide',
      slide: 11,
      skipped: [9, 10],
    });
  });

  it('skips nothing with skip off', () => {
    expect(nextPreview({ slide: 4, step: 0 }, STEPS, DEEP, false)).toEqual({
      kind: 'slide',
      slide: 5,
      skipped: [],
    });
  });

  it('says end on the last slide with every step shown', () => {
    expect(nextPreview({ slide: 11, step: 0 }, STEPS, DEEP, false)).toEqual({ kind: 'end' });
    expect(nextPreview({ slide: 2, step: 0 }, [0, 0, 0], new Set([3]), true)).toEqual({
      kind: 'end',
    });
  });

  it('agrees with nextPosition everywhere', () => {
    for (const skip of [false, true])
      for (let slide = 1; slide <= STEPS.length; slide++)
        for (let step = 0; step <= STEPS[slide - 1]; step++) {
          const pos = { slide, step };
          const preview = nextPreview(pos, STEPS, DEEP, skip);
          const next = nextPosition(pos, STEPS, DEEP, skip);
          if (preview.kind === 'end') expect(next).toBe(pos);
          else
            expect(next).toEqual({
              slide: preview.slide,
              step: preview.kind === 'step' ? preview.step : 0,
            });
        }
  });
});

describe('deepDivesAhead', () => {
  it('counts the deep dives after the current slide', () => {
    expect(deepDivesAhead({ slide: 1, step: 0 }, DEEP)).toBe(3);
    expect(deepDivesAhead({ slide: 5, step: 2 }, DEEP)).toBe(2);
    expect(deepDivesAhead({ slide: 9, step: 0 }, DEEP)).toBe(1);
    expect(deepDivesAhead({ slide: 10, step: 0 }, DEEP)).toBe(0);
    expect(deepDivesAhead({ slide: 1, step: 0 }, NONE)).toBe(0);
  });
});
