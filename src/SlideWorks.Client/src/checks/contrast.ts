import { contrastRatio, mixColours, ratioText } from '../templates/editor/contrast';
import { isChrome, textRunsOf, type CanvasRect, type TextRun } from './canvas';
import { severityOf, type MeasureRule, type MeasuredFinding } from './types';

const LARGE_TEXT_PX = 28;
const SMALL_BAR = 4.5;
const LARGE_BAR = 3;
const WHERE_LENGTH = 40;
const QUOTE_LENGTH = 30;

type Colour = { hex: string; alpha: number };

const hexOfChannels = (channels: number[]) =>
  '#' +
  channels
    .slice(0, 3)
    .map((c) =>
      Math.min(255, Math.max(0, Math.round(c)))
        .toString(16)
        .padStart(2, '0'),
    )
    .join('');

// Reads a computed colour. Chromium gives rgb()/rgba() with channels 0-255, and color(srgb r g b / a) with channels
// 0-1 for a color-mix(). The colours come from getComputedStyle; none is written here.
function parseColour(value: string): Colour | undefined {
  const text = value.trim();
  const rgb = /^rgba?\(([^)]+)\)$/.exec(text);
  const srgb = /^color\(srgb\s+([^)]+)\)$/.exec(text);
  const match = rgb ?? srgb;
  if (!match) return undefined;
  const parts = match[1]!
    .split(/[\s,/]+/)
    .filter(Boolean)
    .map((p) => (p.endsWith('%') ? Number(p.slice(0, -1)) / 100 : Number(p)));
  if (parts.length < 3 || parts.some((p) => Number.isNaN(p))) return undefined;
  const channels = srgb ? parts.slice(0, 3).map((c) => c * 255) : parts.slice(0, 3);
  return { hex: hexOfChannels(channels), alpha: parts[3] ?? 1 };
}

const quote = (text: string) =>
  text.length > QUOTE_LENGTH ? `${text.slice(0, QUOTE_LENGTH)}…` : text;

const overlaps = (a: CanvasRect, b: CanvasRect) =>
  a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

// True when the run's box overlaps an image, or an ancestor up to the frame draws a background image or gradient.
function onImage(run: TextRun, frame: HTMLElement, images: { rect: CanvasRect }[]): boolean {
  if (images.some((img) => run.rects.some((r) => overlaps(r, img.rect)))) return true;
  for (let el: HTMLElement | null = run.element; el; el = el.parentElement) {
    if (getComputedStyle(el).backgroundImage !== 'none') return true;
    if (
      getComputedStyle(el, '::before').backgroundImage !== 'none' ||
      getComputedStyle(el, '::after').backgroundImage !== 'none'
    )
      return true;
    if (el === frame) break;
  }
  return false;
}

// The colour behind a run: the nearest solid ancestor background (ending at the frame), with every translucent layer
// above it blended in, nearest the base first.
function backgroundOf(run: TextRun, frame: HTMLElement): string | undefined {
  const layers: Colour[] = [];
  for (let el: HTMLElement | null = run.element; el; el = el.parentElement) {
    const colour = parseColour(getComputedStyle(el).backgroundColor);
    if (colour && colour.alpha === 1) {
      return layers.reduceRight(
        (under, layer) => mixColours(layer.hex, under, layer.alpha),
        colour.hex,
      );
    }
    if (colour && colour.alpha > 0) layers.push(colour);
    if (el === frame) break;
  }
  return undefined;
}

// The colour the glyphs are painted in, and the bar it must reach. Transparent text with a -webkit-text-stroke is
// painted by its stroke (held to the large bar); SVG text is painted by fill.
function paintOf(el: HTMLElement, size: number): { colour: Colour; bar: number } | undefined {
  const style = getComputedStyle(el);
  const bar = size >= LARGE_TEXT_PX ? LARGE_BAR : SMALL_BAR;
  if (el instanceof SVGElement) {
    const fill = parseColour(style.fill);
    return fill && fill.alpha > 0 ? { colour: fill, bar } : undefined;
  }
  const fore = parseColour(style.color);
  if (fore && fore.alpha > 0) return { colour: fore, bar };
  const stroke = parseColour(style.getPropertyValue('-webkit-text-stroke-color'));
  if (
    parseFloat(style.getPropertyValue('-webkit-text-stroke-width')) > 0 &&
    stroke &&
    stroke.alpha > 0
  )
    return { colour: stroke, bar: LARGE_BAR };
  return undefined;
}

// Text whose colour is too close to its background, and text drawn on an image or gradient (noted, not measured).
export const contrast: MeasureRule = (frame, slide, canvas) => {
  const images = Array.from(frame.querySelectorAll('img, video'))
    .filter((img) => !isChrome(img))
    .map((img) => ({ rect: canvas.toCanvas(img.getBoundingClientRect()) }));
  const found: MeasuredFinding[] = [];
  const seen = new Set<HTMLElement>();
  for (const run of textRunsOf(frame)) {
    if (run.rects.length === 0 || seen.has(run.element)) continue;
    seen.add(run.element);
    const where = `text "${run.text.slice(0, WHERE_LENGTH)}"`;
    if (onImage(run, frame, images)) {
      found.push({
        slide,
        rule: 'textOnImage',
        severity: severityOf('textOnImage'),
        source: 'measured',
        where,
        message: `Text "${quote(run.text)}" sits on an image or gradient, so its contrast can't be checked. Look at it in preview_deck.`,
      });
      continue;
    }
    const size = parseFloat(getComputedStyle(run.element).fontSize);
    const paint = paintOf(run.element, size);
    const back = backgroundOf(run, frame);
    // No paint colour means the text is drawn some other way (a clipped fill, a gradient fill), so there is nothing to compare.
    if (!paint || !back) continue;
    const bar = paint.bar;
    const ratio = contrastRatio(mixColours(paint.colour.hex, back, paint.colour.alpha), back);
    if (ratio >= bar) continue;
    found.push({
      slide,
      rule: 'lowContrast',
      severity: severityOf('lowContrast'),
      source: 'measured',
      where,
      message: `Text "${quote(run.text)}" has contrast ${ratioText(ratio)} on its background; it needs ${bar.toFixed(1)}:1. Use a palette colour made for text.`,
    });
  }
  return found;
};
