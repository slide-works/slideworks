import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { canvasOf } from './canvas';
import { textSize } from './textSize';

// jsdom has no layout, so rects are stubbed (as overflow.test.ts does).
const rect = { left: 100, top: 100, right: 600, bottom: 160, width: 500, height: 60 } as DOMRect;

beforeEach(() => {
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    return this.getAttribute('data-testid') === 'slide-frame'
      ? ({ left: 0, top: 0, right: 1920, bottom: 1080, width: 1920, height: 1080 } as DOMRect)
      : rect;
  });
  Range.prototype.getClientRects = function () {
    return [rect] as unknown as DOMRectList;
  };
});

afterEach(() => {
  vi.restoreAllMocks();
});

function frameOf(...children: Element[]) {
  const frame = document.createElement('div');
  frame.setAttribute('data-testid', 'slide-frame');
  frame.append(...children);
  document.body.append(frame);
  return frame;
}

function el(tag: string, text: string, size: string, attrs: Record<string, string> = {}) {
  const node = document.createElement(tag);
  node.textContent = text;
  node.style.fontSize = size;
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
  return node;
}

const run = (frame: HTMLElement) => textSize(frame, 4, canvasOf(frame));

test('reports text under 20 px', () => {
  const found = run(frameOf(el('p', 'Tiny label', '16px'), el('p', 'Fine', '20px')));
  expect(found).toHaveLength(1);
  expect(found[0]).toMatchObject({
    rule: 'smallText',
    severity: 'warning',
    source: 'measured',
    slide: 4,
  });
  expect(found[0]!.message).toContain('drawn at 16 px');
});

test('skips text marked as designed', () => {
  const found = run(
    frameOf(
      (() => {
        const wrap = el('div', '', '20px', { 'data-check-size': 'designed' });
        wrap.append(el('span', 'Label', '14px'));
        return wrap;
      })(),
    ),
  );
  expect(found).toEqual([]);
});

test('skips chrome', () => {
  const found = run(frameOf(el('div', 'Footer', '12px', { 'data-testid': 'slide-footer' })));
  expect(found).toEqual([]);
});
