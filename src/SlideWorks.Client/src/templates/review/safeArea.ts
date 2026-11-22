import { undrawnOf } from './undrawn';
import { bodyOf, scaleOf, textNodesOf, textStart } from './textRuns';

// The safe area on the 1920 × 1080 canvas, in whole pixels (D9): nothing of the layout's own may
// stand outside it.
export const SAFE_AREA = { left: 120, right: 1800, top: 150, bottom: 996 } as const;

const MAX_FINDINGS = 20;
// A box within half a pixel of the edge is inside: boxes are fractional, the figures are whole.
const SLACK = 0.5;

type Box = { left: number; top: number; right: number; bottom: number };

const leaves = ({ left, top, right, bottom }: Box): boolean =>
  left < SAFE_AREA.left - SLACK ||
  top < SAFE_AREA.top - SLACK ||
  right > SAFE_AREA.right + SLACK ||
  bottom > SAFE_AREA.bottom + SLACK;

const finding = (what: string, { left, top, right, bottom }: Box): string =>
  `${what} at x ${Math.round(left)}–${Math.round(right)}, y ${Math.round(top)}–${Math.round(bottom)}`;

// Where the layout's own elements and text stand outside the safe area, as "<class or text> at x
// a–b, y c–d" in canvas pixels, at most 20. `frame` is the slide frame, drawn at any scale: boxes are
// divided by its width over 1920. Only what is under the layout's own wrapper counts, so the
// frame's footer, tracker and progress bar are never listed. A box the layout's root clips still
// counts: the browser reports the box, not what is left visible.
export function overflowOf(frame: HTMLElement): string[] {
  const body = bodyOf(frame);
  if (body === null) return [undrawnOf(frame, 'maximum') ?? 'The layout was not drawn'];
  const origin = frame.getBoundingClientRect();
  const scale = scaleOf(frame);
  const canvas = (rect: DOMRect): Box => ({
    left: (rect.left - origin.left) / scale,
    top: (rect.top - origin.top) / scale,
    right: (rect.right - origin.left) / scale,
    bottom: (rect.bottom - origin.top) / scale,
  });
  const found: string[] = [];
  const add = (what: string, rect: DOMRect) => {
    if (rect.width <= 0 || rect.height <= 0 || found.length >= MAX_FINDINGS) return;
    const box = canvas(rect);
    if (leaves(box)) found.push(finding(what, box));
  };

  for (const element of body.querySelectorAll('*')) {
    const name = element.classList.item(0) ?? element.tagName.toLowerCase();
    add(name, element.getBoundingClientRect());
  }
  for (const node of textNodesOf(body)) {
    const range = node.ownerDocument.createRange();
    range.selectNodeContents(node);
    for (const rect of range.getClientRects()) add(textStart(node.textContent ?? ''), rect);
  }
  return found.slice(0, MAX_FINDINGS);
}
