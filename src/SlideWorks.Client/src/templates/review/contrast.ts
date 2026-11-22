import { relativeLuminance } from '../editor/contrast';
import { bodyOf, textNodesOf, textStart } from './textRuns';

// Text of this size on the canvas, in px, needs the lower bar (WCAG large text).
export const LARGE_TEXT_PX = 28;
export const SMALL_TEXT_NEEDS = 4.5;
export const LARGE_TEXT_NEEDS = 3.0;

type Colour = { rgb: string; alpha: number };

const CHANNELS = '(\\d+(?:\\.\\d+)?)';
const RGB = new RegExp(
  `^rgba?\\(\\s*${CHANNELS}[,\\s]+${CHANNELS}[,\\s]+${CHANNELS}(?:\\s*[,/]\\s*(\\d*\\.?\\d+)(%?))?\\s*\\)$`,
);

// A computed colour (rgb(r, g, b), rgba(…) or rgb(r g b / a)) as the opaque form editor/contrast.ts
// reads, and its alpha; null for anything else.
function colourOf(value: string): Colour | null {
  const match = RGB.exec(value.trim());
  if (match === null) return null;
  const [r, g, b] = [match[1], match[2], match[3]].map((channel) => Math.round(Number(channel)));
  const alpha = match[4] === undefined ? 1 : Number(match[4]) / (match[5] === '%' ? 100 : 1);
  return { rgb: `rgb(${r} ${g} ${b})`, alpha };
}

function ratioOf(fore: string, back: string): number | null {
  const a = relativeLuminance(fore);
  const b = relativeLuminance(back);
  return a === null || b === null ? null : (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

// What one text run sits on: a solid colour, a gradient (not checked), or nothing known.
type Ground = { kind: 'solid'; rgb: string } | { kind: 'gradient' } | { kind: 'unknown' };

// The nearest ancestor with a background wins: an image (a gradient) before a colour of the same
// element; a transparent colour is looked through. The frame is the last to ask.
function groundOf(from: HTMLElement, frame: HTMLElement): Ground {
  for (let element: HTMLElement | null = from; element !== null; element = element.parentElement) {
    const style = getComputedStyle(element);
    if (style.backgroundImage !== '' && style.backgroundImage !== 'none') {
      return { kind: 'gradient' };
    }
    const colour = colourOf(style.backgroundColor);
    if (colour !== null && colour.alpha > 0) return { kind: 'solid', rgb: colour.rgb };
    if (element === frame) break;
  }
  return { kind: 'unknown' };
}

type Run = { node: Text; element: HTMLElement; ground: Ground };

function runsOf(frame: HTMLElement): Run[] {
  const body = bodyOf(frame);
  if (body === null) return [];
  return textNodesOf(body).flatMap((node) => {
    const element = node.parentElement;
    return element === null ? [] : [{ node, element, ground: groundOf(element, frame) }];
  });
}

// The text under the layout's own wrapper whose colour is too close to what it sits on: "<text
// start> 3.2:1, needs 4.5", 4.5 under 28 px on the canvas and 3.0 from there. The text's computed
// colour is compared with the solid background of its nearest ancestor that has one, the frame's
// when none does. Text on a gradient is not checked (see gradientTextsOf). The slide may be scaled:
// computed sizes are the canvas's.
export function lowContrastOf(frame: HTMLElement): string[] {
  const found: string[] = [];
  for (const { node, element, ground } of runsOf(frame)) {
    if (ground.kind !== 'solid') continue;
    const style = getComputedStyle(element);
    const fore = colourOf(style.color);
    if (fore === null) continue;
    const needed =
      parseFloat(style.fontSize) >= LARGE_TEXT_PX ? LARGE_TEXT_NEEDS : SMALL_TEXT_NEEDS;
    const ratio = ratioOf(fore.rgb, ground.rgb);
    if (ratio === null || ratio >= needed) continue;
    const shown = (Math.floor(ratio * 10 + 1e-9) / 10).toFixed(1);
    found.push(`${textStart(node.textContent ?? '')} ${shown}:1, needs ${needed.toFixed(1)}`);
  }
  return found;
}

// How many text runs sit on a gradient and so were not checked: the page tells the owner to look.
export function gradientTextsOf(frame: HTMLElement): number {
  return runsOf(frame).filter(({ ground }) => ground.kind === 'gradient').length;
}
