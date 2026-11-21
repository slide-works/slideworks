import { CANVAS_HEIGHT, CANVAS_WIDTH, footerTop, isChrome, textRunsOf } from './canvas';
import type { CanvasRect } from './canvas';
import { severityOf, type MeasureRule, type SlideFinding } from './types';

const TOLERANCE = 0.5;
const WHERE_LENGTH = 40;

const whole = (value: number) => Math.round(value);

// What a box does wrong, as a message, or undefined when it stays on the slide.
function problem(
  rect: CanvasRect,
  floor: number,
  what: 'Text' | 'Image' | 'Shape',
): string | undefined {
  const fix =
    what === 'Text' ? ' Shorten it or split the slide.' : ' Make it smaller or split the slide.';
  if (rect.bottom > floor + TOLERANCE) {
    return `${what} runs below the footer (y ${whole(rect.top)}–${whole(rect.bottom)}; the footer starts at y ${whole(floor)}).${fix}`;
  }
  if (rect.top < -TOLERANCE) {
    return `${what} runs off the top edge (y ${whole(rect.top)}–${whole(rect.bottom)}).${fix}`;
  }
  if (rect.right > CANVAS_WIDTH + TOLERANCE) {
    return `${what} runs off the right edge (x ${whole(rect.left)}–${whole(rect.right)}).${fix}`;
  }
  if (rect.left < -TOLERANCE) {
    return `${what} runs off the left edge (x ${whole(rect.left)}–${whole(rect.right)}).${fix}`;
  }
  return undefined;
}

const outside = (rect: CanvasRect, floor: number) =>
  rect.left < -TOLERANCE ||
  rect.top < -TOLERANCE ||
  rect.right > CANVAS_WIDTH + TOLERANCE ||
  rect.bottom > floor + TOLERANCE;

const isEmpty = (rect: CanvasRect) => rect.right === rect.left && rect.bottom === rect.top;

// Text, images and top-level SVGs drawn outside the canvas or below the footer. One finding per element.
export const overflow: MeasureRule = (frame, slide, canvas) => {
  const floor = Math.min(footerTop(frame), CANVAS_HEIGHT);
  const found: SlideFinding[] = [];
  const add = (where: string, message: string) =>
    found.push({
      slide,
      rule: 'overflow',
      severity: severityOf('overflow'),
      source: 'measured',
      where,
      message,
    });

  for (const run of textRunsOf(frame)) {
    const rect = run.rects.find((r) => !isEmpty(r) && outside(r, floor));
    if (!rect) continue;
    const message = problem(rect, floor, 'Text');
    if (message) add(`text "${run.text.slice(0, WHERE_LENGTH)}"`, message);
  }

  for (const el of frame.querySelectorAll('img, svg')) {
    if (isChrome(el) || el.parentElement?.closest('svg')) continue;
    const rect = canvas.toCanvas(el.getBoundingClientRect());
    if (isEmpty(rect) || !outside(rect, floor)) continue;
    const tag = el.tagName.toLowerCase();
    const message = problem(rect, floor, tag === 'img' ? 'Image' : 'Shape');
    if (message) add(tag, message);
  }
  return found;
};
