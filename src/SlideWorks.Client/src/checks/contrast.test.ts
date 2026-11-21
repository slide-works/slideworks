import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { canvasOf } from './canvas';
import { contrast } from './contrast';

// jsdom has no layout, so every rect is stubbed (as overflow.test.ts does). Boxes are in canvas px at scale 1.
type Box = [left: number, top: number, right: number, bottom: number];
const boxes = new WeakMap<Element, Box>();

const domRect = ([left, top, right, bottom]: Box): DOMRect =>
  ({ left, top, right, bottom, width: right - left, height: bottom - top }) as DOMRect;

beforeEach(() => {
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    return domRect(boxes.get(this) ?? [0, 0, 0, 0]);
  });
  Range.prototype.getClientRects = function (this: Range) {
    const box = boxes.get(this.startContainer.parentElement!);
    return (box ? [domRect(box)] : []) as unknown as DOMRectList;
  };
});

afterEach(() => {
  vi.restoreAllMocks();
});

// Colours are built from channels, so no colour literal sits in this folder.
const grey = (level: number) => `rgb(${level}, ${level}, ${level})`;

function textEl(
  text: string,
  style: Partial<CSSStyleDeclaration>,
  box: Box = [100, 100, 600, 160],
) {
  const el = document.createElement('p');
  el.textContent = text;
  Object.assign(el.style, style);
  boxes.set(el, box);
  return el;
}

function frameOf(style: Partial<CSSStyleDeclaration>, children: Element[]) {
  const frame = document.createElement('div');
  Object.assign(frame.style, style);
  boxes.set(frame, [0, 0, 1920, 1080]);
  frame.append(...children);
  document.body.append(frame);
  return frame;
}

const run = (frame: HTMLElement) => contrast(frame, 2, canvasOf(frame));

test('reports small text under 4.5', () => {
  const frame = frameOf({ backgroundColor: grey(255) }, [
    textEl('Quarterly results', { color: grey(150), fontSize: '24px' }),
  ]);
  const found = run(frame);
  expect(found).toHaveLength(1);
  expect(found[0]).toMatchObject({
    rule: 'lowContrast',
    severity: 'warning',
    source: 'measured',
    slide: 2,
  });
  expect(found[0]!.message).toContain('it needs 4.5:1');
});

test('passes large text at 3.0', () => {
  // grey 140 on white is about 3.4:1, enough for text at 28 px and over, not for smaller text.
  const frame = frameOf({ backgroundColor: grey(255) }, [
    textEl('Quarterly results', { color: grey(140), fontSize: '40px' }),
  ]);
  expect(run(frame)).toEqual([]);
});

test("uses the frame's background when no ancestor has one", () => {
  const frame = frameOf({ backgroundColor: grey(0) }, [
    textEl('Dark on dark', { color: grey(20), fontSize: '24px' }),
  ]);
  const found = run(frame);
  expect(found).toHaveLength(1);
  expect(found[0]!.rule).toBe('lowContrast');
});

test('notes text on a gradient instead of measuring it', () => {
  const band = document.createElement('div');
  band.style.backgroundImage = 'linear-gradient(to right, black, white)';
  band.append(textEl('Over a gradient', { color: grey(150), fontSize: '24px' }));
  const frame = frameOf({ backgroundColor: grey(255) }, [band]);
  const found = run(frame);
  expect(found).toHaveLength(1);
  expect(found[0]).toMatchObject({ rule: 'textOnImage', severity: 'note' });
});

test('notes text over an image', () => {
  const img = document.createElement('img');
  boxes.set(img, [0, 0, 1000, 600]);
  const frame = frameOf({ backgroundColor: grey(255) }, [
    img,
    textEl('Over a photo', { color: grey(150), fontSize: '24px' }),
  ]);
  const found = run(frame);
  expect(found).toHaveLength(1);
  expect(found[0]).toMatchObject({ rule: 'textOnImage', severity: 'note' });
});
