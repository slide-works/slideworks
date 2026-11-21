import { contrastRatio, ratioText } from '../templates/editor/contrast';
import { isChrome, textRunsOf, type CanvasRect, type TextRun } from './canvas';
import { severityOf, type MeasureRule, type SlideFinding } from './types';

const LARGE_TEXT_PX = 28;
const SMALL_BAR = 4.5;
const LARGE_BAR = 3;
const WHERE_LENGTH = 40;
const QUOTE_LENGTH = 30;

type Colour = { hex: string; alpha: number };

// Reads a computed rgb()/rgba() string. The colours come from getComputedStyle; none is written here.
function parseColour(value: string): Colour | undefined {
  const match = /^rgba?\(([^)]+)\)$/.exec(value.trim());
  if (!match) return undefined;
  const parts = match[1]!
    .split(/[\s,/]+/)
    .filter(Boolean)
    .map(Number);
  if (parts.length < 3 || parts.some((p) => Number.isNaN(p))) return undefined;
  const hex =
    '#' +
    parts
      .slice(0, 3)
      .map((c) => Math.round(c).toString(16).padStart(2, '0'))
      .join('');
  return { hex, alpha: parts[3] ?? 1 };
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
    if (el === frame) break;
  }
  return false;
}

// The nearest ancestor's opaque background colour, ending at the frame.
function backgroundOf(run: TextRun, frame: HTMLElement): string | undefined {
  for (let el: HTMLElement | null = run.element; el; el = el.parentElement) {
    const colour = parseColour(getComputedStyle(el).backgroundColor);
    if (colour && colour.alpha === 1) return colour.hex;
    if (el === frame) break;
  }
  return undefined;
}

// Text whose colour is too close to its background, and text drawn on an image or gradient (noted, not measured).
export const contrast: MeasureRule = (frame, slide, canvas) => {
  const images = Array.from(frame.querySelectorAll('img'))
    .filter((img) => !isChrome(img))
    .map((img) => ({ rect: canvas.toCanvas(img.getBoundingClientRect()) }));
  const found: SlideFinding[] = [];
  const seen = new Set<HTMLElement>();
  for (const run of textRunsOf(frame)) {
    if (seen.has(run.element)) continue;
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
    const style = getComputedStyle(run.element);
    const fore = parseColour(style.color);
    const back = backgroundOf(run, frame);
    if (!fore || !back) continue;
    const bar = parseFloat(style.fontSize) >= LARGE_TEXT_PX ? LARGE_BAR : SMALL_BAR;
    const ratio = contrastRatio(fore.hex, back);
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
