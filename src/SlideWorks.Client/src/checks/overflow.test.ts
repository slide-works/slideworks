import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { canvasOf } from './canvas';
import { overflow } from './overflow';

// jsdom has no layout, so every rect is stubbed. Boxes are given in canvas px and multiplied by the frame's scale.
type Box = [left: number, top: number, right: number, bottom: number];
const boxes = new WeakMap<Element, Box[]>();
let scale = 1;

const domRect = ([left, top, right, bottom]: Box): DOMRect =>
  ({
    left: left * scale,
    top: top * scale,
    right: right * scale,
    bottom: bottom * scale,
    width: (right - left) * scale,
    height: (bottom - top) * scale,
  }) as DOMRect;

beforeEach(() => {
  scale = 1;
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    return domRect(boxes.get(this)?.[0] ?? [0, 0, 0, 0]);
  });
  Range.prototype.getClientRects = function (this: Range) {
    const owner = this.startContainer.parentElement!;
    return (boxes.get(owner) ?? []).map(domRect) as unknown as DOMRectList;
  };
});

afterEach(() => {
  vi.restoreAllMocks();
});

function frameOf(children: Element[]) {
  const frame = document.createElement('div');
  boxes.set(frame, [[0, 0, 1920, 1080]]);
  frame.append(...children);
  return frame;
}

function text(content: string, ...rects: Box[]) {
  const p = document.createElement('p');
  p.textContent = content;
  boxes.set(p, rects);
  return p;
}

function run(frame: HTMLElement) {
  // The frame's box is set before the scale is read.
  return overflow(frame, 3, canvasOf(frame));
}

const footer = () => {
  const el = document.createElement('div');
  el.dataset.testid = 'slide-footer';
  el.setAttribute('data-testid', 'slide-footer');
  boxes.set(el, [[0, 1023, 1920, 1080]]);
  return el;
};

test('reports text below the footer', () => {
  const frame = frameOf([text('Too much', [120, 1031, 600, 1098]), footer()]);

  const found = run(frame);

  expect(found).toEqual([
    {
      slide: 3,
      rule: 'overflow',
      severity: 'error',
      source: 'measured',
      where: 'text "Too much"',
      message:
        'Text runs below the footer (y 1031–1098; the footer starts at y 1023). Shorten it or split the slide.',
    },
  ]);
});

test('reports text past the right edge', () => {
  const frame = frameOf([text('Wide', [1802, 200, 1960, 260])]);

  const found = run(frame);

  expect(found.map((f) => f.message)).toEqual([
    'Text runs off the right edge (x 1802–1960). Shorten it or split the slide.',
  ]);
});

test('ignores chrome', () => {
  const tracker = text('Part one', [0, 1500, 100, 1600]);
  tracker.setAttribute('data-testid', 'part-tracker');

  const found = run(frameOf([tracker]));

  expect(found).toEqual([]);
});

test('reports an element once however many lines overflow', () => {
  const frame = frameOf([
    text('Long text', [120, 1000, 600, 1060], [120, 1060, 600, 1120], [120, 1120, 600, 1180]),
  ]);

  const found = run(frame);

  expect(found).toHaveLength(1);
});

test("scales by the frame's width", () => {
  scale = 0.5;
  const frame = frameOf([text('Half size', [120, 1031, 600, 1098]), footer()]);

  const found = run(frame);

  expect(found[0].message).toContain('y 1031–1098; the footer starts at y 1023');
});

test('reports nothing when everything fits', () => {
  const frame = frameOf([text('Fits', [120, 200, 600, 260]), footer()]);

  expect(run(frame)).toEqual([]);
});
