import type { Page } from 'playwright/test';
import type { Box } from './box';

// The extent of everything a layout draws, in canvas px from the slide's top
// left: every element's box and every run of text (text that overflows its
// element is caught by its own rects). Text hidden by an ancestor that clips
// its overflow, such as a clamped journey name, is cut to that ancestor's box.
// Elements matching `exclude` (and what is inside them) are left out.
export async function drawnBox(page: Page, layoutTestId: string, exclude?: string): Promise<Box> {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element, excluded: string | undefined) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const box = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const skip = (element: Element | null) =>
      excluded !== undefined && element?.closest(excluded) != null;
    const add = (rect: { left: number; top: number; right: number; bottom: number }) => {
      if (rect.right - rect.left <= 0 && rect.bottom - rect.top <= 0) return;
      box.left = Math.min(box.left, rect.left - slide.left);
      box.top = Math.min(box.top, rect.top - slide.top);
      box.right = Math.max(box.right, rect.right - slide.left);
      box.bottom = Math.max(box.bottom, rect.bottom - slide.top);
    };
    for (const element of root.querySelectorAll('*')) {
      if (!skip(element)) add(element.getBoundingClientRect());
    }
    // A line's glyphs can rise a little above its line box (a 20 px/1 kicker,
    // the 240 px/0.9 part number) while the ink stays inside it, so the top is
    // taken from the boxes, as in ContentLayouts.spec.tsx.
    const boxTop = box.top;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      if (skip(node.parentElement)) continue;
      const clips: DOMRect[] = [];
      for (let el = node.parentElement; el !== null && el !== root; el = el.parentElement) {
        if (getComputedStyle(el).overflow !== 'visible') clips.push(el.getBoundingClientRect());
      }
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) {
        let { left, top, right, bottom } = rect;
        for (const clip of clips) {
          left = Math.max(left, clip.left);
          top = Math.max(top, clip.top);
          right = Math.min(right, clip.right);
          bottom = Math.min(bottom, clip.bottom);
        }
        if (right > left && bottom > top) add({ left, top, right, bottom });
      }
    }
    return { ...box, top: boxTop };
  }, exclude);
}
