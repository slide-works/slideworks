import { useLayoutEffect, type RefObject } from 'react';

// How display text fits: it is set at `start` px, and when it breaks its budget
// it steps down to the largest size that keeps it, never below `floor`.
export type FitBudget = {
  // The size it is set at when it fits, in canvas px.
  start: number;
  // The smallest size it shrinks to.
  floor: number;
  // At most this many lines of `lineHeight` × the font size.
  lines?: number;
  // The element's line height as a multiple of its font size (default 1).
  lineHeight?: number;
  // At most this tall, in canvas px, read from the layout on each fit. Pass a
  // function declared outside the component, so it stays the same function.
  maxHeight?: (element: HTMLElement) => number;
  // Also no wider than its parent.
  width?: boolean;
};

// A box that measures 0 is waiting for its scale: the deck page's thumbnail
// renders at scale(0) until it knows its width. The fit waits a frame at a
// time for it, up to this many frames, and fits again once fonts load.
const MAX_WAIT_FRAMES = 60;

// The largest whole size from `floor` to `start` that `fits`, or `floor` when
// none does. It tries `start` first, then halves the range, so it measures
// about log2(start - floor) times, not once per pixel.
export function largestFit(start: number, floor: number, fits: (size: number) => boolean): number {
  if (fits(start)) return start;
  let low = floor;
  let high = start - 1;
  while (low < high) {
    const middle = Math.ceil((low + high) / 2);
    if (fits(middle)) low = middle;
    else high = middle - 1;
  }
  return low;
}

// Fits the text in `ref` to its budget by setting its font size. It fits on
// mount, again after document.fonts.ready, and again when `text` or the budget
// changes; never on any other render. Heights are layout px (offsetHeight), so
// the player's scaling doesn't change them; the width check compares the
// element's box with its parent's, both under the same transform.
export function useFitText(ref: RefObject<HTMLElement | null>, budget: FitBudget, text: string) {
  const { start, floor, lines, lineHeight = 1, maxHeight, width = false } = budget;

  useLayoutEffect(() => {
    const element = ref.current;
    if (element === null) return;
    let active = true;
    let frame = 0;
    let waited = 0;

    const fits = (size: number) => {
      element.style.fontSize = `${size}px`;
      const height = element.offsetHeight;
      // One px of slack for the rounding in offsetHeight.
      if (lines !== undefined && height > lines * lineHeight * size + 1) return false;
      if (maxHeight !== undefined && height > maxHeight(element)) return false;
      if (width && element.parentElement !== null) {
        return (
          element.getBoundingClientRect().width <=
          element.parentElement.getBoundingClientRect().width
        );
      }
      return true;
    };

    const fit = () => {
      if (!active) return;
      const box = element.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) {
        if (waited < MAX_WAIT_FRAMES) {
          waited += 1;
          frame = requestAnimationFrame(fit);
        }
        return;
      }
      element.style.fontSize = `${largestFit(start, floor, fits)}px`;
    };

    fit();
    void document.fonts.ready.then(fit);
    return () => {
      active = false;
      cancelAnimationFrame(frame);
    };
  }, [ref, text, start, floor, lines, lineHeight, maxHeight, width]);
}
