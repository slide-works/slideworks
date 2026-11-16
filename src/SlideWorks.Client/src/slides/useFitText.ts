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
  // Also no wider than its parent. The element must be a block or inline-block.
  width?: boolean;
};

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

// An element's used width in layout px. Unlike its bounding box, it ignores
// transforms, so a slide drawn at scale(0) measures the same as at scale(1).
function layoutWidth(element: Element): number {
  return parseFloat(getComputedStyle(element).width);
}

// Whether the element is no wider than its parent, in layout px; through their
// boxes (both under the same transform) when either has no used width.
function fitsParentWidth(element: HTMLElement, parent: HTMLElement): boolean {
  const own = layoutWidth(element);
  const room = layoutWidth(parent);
  if (Number.isFinite(own) && Number.isFinite(room)) return own <= room;
  return element.getBoundingClientRect().width <= parent.getBoundingClientRect().width;
}

// Fits the text in `ref` to its budget by setting its font size. It fits on
// mount, again after document.fonts.ready, and again when `text` or the budget
// changes; never on any other render.
//
// Every measure is in layout px (offsetHeight, used widths), so the player's
// scaling and the deck page's thumbnail, which draws the slide at scale(0)
// until it knows its width, don't change it. A box with no layout size (inside
// a display: none or zero-width container) can't be measured: the fit then
// waits for it with a ResizeObserver, however long that takes, and fits once
// the box has a size.
export function useFitText(ref: RefObject<HTMLElement | null>, budget: FitBudget, text: string) {
  const { start, floor, lines, lineHeight = 1, maxHeight, width = false } = budget;

  useLayoutEffect(() => {
    const element = ref.current;
    if (element === null) return;
    let active = true;
    // Set only while the box has no size; one observer however often the fit
    // is asked for meanwhile (on mount, and again when fonts load).
    let waiting: ResizeObserver | null = null;

    const fits = (size: number) => {
      element.style.fontSize = `${size}px`;
      const height = element.offsetHeight;
      // One px of slack for the rounding in offsetHeight.
      if (lines !== undefined && height > lines * lineHeight * size + 1) return false;
      if (maxHeight !== undefined && height > maxHeight(element)) return false;
      if (width && element.parentElement !== null) {
        return fitsParentWidth(element, element.parentElement);
      }
      return true;
    };

    const fit = () => {
      if (!active) return;
      if (element.offsetWidth === 0 || element.offsetHeight === 0) {
        if (waiting === null) {
          waiting = new ResizeObserver(fit);
          waiting.observe(element);
        }
        return;
      }
      waiting?.disconnect();
      waiting = null;
      element.style.fontSize = `${largestFit(start, floor, fits)}px`;
    };

    fit();
    void document.fonts.ready.then(fit);
    return () => {
      active = false;
      waiting?.disconnect();
    };
  }, [ref, text, start, floor, lines, lineHeight, maxHeight, width]);
}
