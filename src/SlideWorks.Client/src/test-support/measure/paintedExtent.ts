import type { Page } from 'playwright/test';
import type { Box } from './box';

export type PaintedExtentOptions = {
  /** Count an <img> as drawn even when it has no background, shadow or border. */
  images?: boolean;
  /**
   * A selector for text whose font box runs past its ink (a big numeral on a
   * short line). Its left and right come from its own box and its bottom from
   * the baseline, found with a zero-height probe.
   */
  baselineText?: string;
};

/**
 * What a layout draws, in canvas px from the slide's top left: every run of
 * text and every painted box (a background, shadow or border; an image when
 * `images` is set) inside the layout. Layout boxes that only stretch to fill
 * the body, such as a column or the body itself, are left out, so a column of
 * short text is measured where its text ends.
 *
 * Returns `drawn`, `boxTop` (the top of every element box: a 20 px/1 kicker's
 * glyphs rise a little above its line box, the boxes don't), `body` (the
 * `.sw-body` box) and `footerTop`. It asserts nothing.
 */
export async function paintedExtent(
  page: Page,
  layoutTestId: string,
  options: PaintedExtentOptions = {},
) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element, opts: PaintedExtentOptions) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const rel = (rect: DOMRect): Box => ({
      left: rect.left - slide.left,
      top: rect.top - slide.top,
      right: rect.right - slide.left,
      bottom: rect.bottom - slide.top,
    });
    const drawn = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const add = (rect: DOMRect) => {
      if (rect.width === 0 && rect.height === 0) return;
      const box = rel(rect);
      drawn.left = Math.min(drawn.left, box.left);
      drawn.top = Math.min(drawn.top, box.top);
      drawn.right = Math.max(drawn.right, box.right);
      drawn.bottom = Math.max(drawn.bottom, box.bottom);
    };
    let boxTop = Infinity;
    for (const element of root.querySelectorAll('*')) {
      const rect = element.getBoundingClientRect();
      if (rect.width > 0 || rect.height > 0) boxTop = Math.min(boxTop, rel(rect).top);
      const style = getComputedStyle(element);
      const painted =
        (opts.images === true && element.tagName === 'IMG') ||
        style.backgroundColor !== 'rgba(0, 0, 0, 0)' ||
        style.boxShadow !== 'none' ||
        parseFloat(style.borderTopWidth) > 0;
      if (painted) add(rect);
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      if (opts.baselineText !== undefined && node.parentElement?.matches(opts.baselineText)) {
        const probe = document.createElement('span');
        probe.style.display = 'inline-block';
        probe.style.height = '0';
        node.parentElement.append(probe);
        const baseline = rel(probe.getBoundingClientRect()).bottom;
        probe.remove();
        const numeral = rel(node.parentElement.getBoundingClientRect());
        drawn.left = Math.min(drawn.left, numeral.left);
        drawn.right = Math.max(drawn.right, numeral.right);
        drawn.bottom = Math.max(drawn.bottom, baseline);
        continue;
      }
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    const body = rel(root.querySelector('.sw-body')!.getBoundingClientRect());
    const footer = document.querySelector('[data-testid="slide-footer"]')!.getBoundingClientRect();
    return { drawn, boxTop, body, footerTop: footer.top - slide.top };
  }, options);
}

/**
 * The box of the first (or, with `last`, the last) element matching `selector`
 * inside a layout, in canvas px from the slide's top left.
 */
export async function slideBox(
  page: Page,
  layoutTestId: string,
  selector: string,
  last = false,
): Promise<Box> {
  return page.getByTestId(layoutTestId).evaluate(
    (root: Element, args: { selector: string; last: boolean }) => {
      const slide = root.closest('[data-layout]')!.getBoundingClientRect();
      const all = root.querySelectorAll(args.selector);
      const rect = all[args.last ? all.length - 1 : 0]!.getBoundingClientRect();
      return {
        left: rect.left - slide.left,
        top: rect.top - slide.top,
        right: rect.right - slide.left,
        bottom: rect.bottom - slide.top,
      };
    },
    { selector, last },
  );
}
