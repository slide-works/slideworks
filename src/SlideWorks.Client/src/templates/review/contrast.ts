import { relativeLuminance } from '../editor/contrast';
import { bodyOf, textNodesOf, textStart } from './textRuns';
import { undrawnOf } from './undrawn';

// Text of this size on the canvas, in px, needs the lower bar (WCAG large text).
export const LARGE_TEXT_PX = 28;
export const SMALL_TEXT_NEEDS = 4.5;
export const LARGE_TEXT_NEEDS = 3.0;

type Colour = { rgb: string; alpha: number };

const CHANNELS = '(\\d+(?:\\.\\d+)?)';
const RGB = new RegExp(
  `^rgba?\\(\\s*${CHANNELS}[,\\s]+${CHANNELS}[,\\s]+${CHANNELS}(?:\\s*[,/]\\s*(\\d*\\.?\\d+)(%?))?\\s*\\)$`,
);

// A channel of color(srgb …): a number from 0 to 1, a percentage, or "none" (0).
const UNIT = '(none|\\d*\\.?\\d+(?:e[+-]?\\d+)?%?)';
const SRGB = new RegExp(
  `^color\\(\\s*srgb\\s+${UNIT}\\s+${UNIT}\\s+${UNIT}(?:\\s*/\\s*${UNIT})?\\s*\\)$`,
  'i',
);

function unitOf(token: string, scale: number): number {
  if (token.toLowerCase() === 'none') return 0;
  const value = token.endsWith('%') ? parseFloat(token) / 100 : parseFloat(token);
  return Math.min(1, Math.max(0, value)) * scale;
}

// A computed colour (rgb(r, g, b), rgba(…), rgb(r g b / a) or the color(srgb r g b / a) Chromium
// gives a color-mix) as the opaque form editor/contrast.ts reads, and its alpha; null for anything
// else, including every other colour space.
function colourOf(value: string): Colour | null {
  const mixed = SRGB.exec(value.trim());
  if (mixed !== null) {
    const [r, g, b] = [mixed[1], mixed[2], mixed[3]].map((token) => Math.round(unitOf(token, 255)));
    return {
      rgb: `rgb(${r} ${g} ${b})`,
      alpha: mixed[4] === undefined ? 1 : unitOf(mixed[4], 1),
    };
  }
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

// What the layers behind a text run add up to: a colour (premultiplied-free rgb and the alpha the
// layers cover), a gradient or image (not checked), or something that could not be read.
type Backdrop =
  | { kind: 'colour'; rgb: [number, number, number]; alpha: number }
  | { kind: 'gradient' }
  | { kind: 'unreadable' }
  | { kind: 'faded' };

type Ground = Backdrop;

const channelsOf = (rgb: string): [number, number, number] => {
  const [r, g, b] = (rgb.match(/\d+/g) ?? ['0', '0', '0']).map(Number);
  return [r, g, b];
};

const opacityOf = (style: CSSStyleDeclaration): number => {
  const value = parseFloat(style.opacity);
  return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 1;
};

// The backdrop of `from`, composited from the frame down to the element: each element's background
// (its alpha times the opacity of it and every ancestor from the frame down) is laid over what is
// behind it. An opaque layer hides everything below it; a gradient or image stands until one
// does. `opacity` is the product of the opacities from the frame down to `from`, which the text
// takes as well.
function groundOf(from: HTMLElement, frame: HTMLElement): Ground {
  const chain: HTMLElement[] = [];
  for (let element: HTMLElement | null = from; element !== null; element = element.parentElement) {
    chain.unshift(element);
    if (element === frame) break;
  }
  let backdrop: Backdrop = { kind: 'colour', rgb: [0, 0, 0], alpha: 0 };
  let faded = false;
  for (const element of chain) {
    const style = getComputedStyle(element);
    // CSS paints an element with opacity as a group after its children, which this does not model.
    if (opacityOf(style) < 1) faded = true;
    const hasImage = style.backgroundImage !== '' && style.backgroundImage !== 'none';
    const layer = colourOf(style.backgroundColor);
    if (layer === null) {
      backdrop = { kind: 'unreadable' };
      continue;
    }
    const alpha = layer.alpha;
    if (hasImage) {
      backdrop = { kind: 'gradient' };
      continue;
    }
    if (alpha <= 0) continue;
    if (backdrop.kind !== 'colour') {
      // A solid layer hides what is under it; a translucent one over a gradient leaves it unknown.
      if (alpha >= 1) backdrop = { kind: 'colour', rgb: channelsOf(layer.rgb), alpha: 1 };
      continue;
    }
    const fore = channelsOf(layer.rgb);
    const behind: [number, number, number] = backdrop.rgb;
    const behindAlpha: number = backdrop.alpha;
    const total: number = alpha + behindAlpha * (1 - alpha);
    const mix = (i: number): number =>
      (fore[i] * alpha + behindAlpha * behind[i] * (1 - alpha)) / total;
    backdrop = { kind: 'colour', rgb: [mix(0), mix(1), mix(2)], alpha: total };
  }
  return faded ? { kind: 'faded' } : backdrop;
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

const FULLY_COVERED = 0.999;

// The text under the layout's own wrapper whose colour is too close to what it sits on: "<text
// start> 3.2:1, needs 4.5", 4.5 under 28 px on the canvas and 3.0 from there. Colours are the ones
// a person sees: the text's alpha, times the opacity of it and its ancestors, is blended over the
// backdrop composited from the backgrounds of its ancestors. What cannot be worked out (a gradient
// or image behind the text, a colour this cannot read, a backdrop that is not fully covered) is a
// finding too, never a pass. The slide may be scaled: computed sizes are the canvas's.
export function lowContrastOf(frame: HTMLElement): string[] {
  const body = bodyOf(frame);
  if (body === null) return [undrawnOf(frame, 'sample') ?? 'The layout was not drawn'];
  const found: string[] = [];
  for (const { node, element, ground } of runsOf(frame)) {
    const start = textStart(node.textContent ?? '');
    const style = getComputedStyle(element);
    const fore = colourOf(style.color);
    if (fore === null || ground.kind === 'unreadable') {
      found.push(`${start} has a colour that could not be read, so its contrast was not checked`);
      continue;
    }
    if (ground.kind === 'faded') {
      found.push(`${start} sits under opacity, so its contrast can't be measured`);
      continue;
    }
    if (ground.kind === 'gradient') {
      found.push(`${start} sits on a gradient or image, so its contrast was not checked`);
      continue;
    }
    if (ground.alpha < FULLY_COVERED) {
      found.push(`${start} sits on a see-through background, so its contrast was not checked`);
      continue;
    }
    const alpha = fore.alpha;
    const text = channelsOf(fore.rgb);
    const blended = text.map((channel, i) =>
      Math.round(channel * alpha + ground.rgb[i] * (1 - alpha)),
    );
    const needed =
      parseFloat(style.fontSize) >= LARGE_TEXT_PX ? LARGE_TEXT_NEEDS : SMALL_TEXT_NEEDS;
    const ratio = ratioOf(
      `rgb(${blended.join(' ')})`,
      `rgb(${ground.rgb.map(Math.round).join(' ')})`,
    );
    if (ratio === null) {
      found.push(`${start} has a colour that could not be read, so its contrast was not checked`);
      continue;
    }
    if (ratio >= needed) continue;
    const shown = (Math.floor(ratio * 10 + 1e-9) / 10).toFixed(1);
    found.push(`${start} ${shown}:1, needs ${needed.toFixed(1)}`);
  }
  return found;
}

// How many text runs sit on a gradient and so were not checked: the page tells the owner to look.
export function gradientTextsOf(frame: HTMLElement): number {
  return runsOf(frame).filter(({ ground }) => ground.kind === 'gradient').length;
}
