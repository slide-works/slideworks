import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { overflowOf } from './safeArea';

const rect = (left: number, top: number, right: number, bottom: number): DOMRect =>
  ({
    left,
    top,
    right,
    bottom,
    width: right - left,
    height: bottom - top,
    x: left,
    y: top,
    toJSON: () => ({}),
  }) as DOMRect;

const boxed = <T extends HTMLElement>(element: T, box: DOMRect): T => {
  element.getBoundingClientRect = () => box;
  return element;
};

// A frame `width` wide holding the layout's wrapper and a footer outside it.
function frameOf(width = 1920) {
  const frame = boxed(document.createElement('div'), rect(0, 0, width, (width * 9) / 16));
  const body = document.createElement('div');
  body.setAttribute('data-custom-layout-body', '');
  const footer = document.createElement('footer');
  frame.append(body, footer);
  document.body.append(frame);
  return { frame, body, footer };
}

const child = (parent: HTMLElement, className: string, box: DOMRect) => {
  const element = boxed(document.createElement('div'), box);
  element.className = className;
  parent.append(element);
  return element;
};

const originalRects = Range.prototype.getClientRects;
beforeEach(() => {
  Range.prototype.getClientRects = () => [] as unknown as DOMRectList;
});
afterEach(() => {
  Range.prototype.getClientRects = originalRects;
  document.body.replaceChildren();
});

describe('overflowOf', () => {
  it('reports nothing inside the area', () => {
    const { frame, body } = frameOf();
    child(body, 'chart', rect(120, 150, 1800, 996));
    expect(overflowOf(frame)).toEqual([]);
  });

  it('reports a box past x 1800', () => {
    const { frame, body } = frameOf();
    child(body, 'bar', rect(200, 300, 1850, 400));
    expect(overflowOf(frame)).toEqual(['bar at x 200–1850, y 300–400']);
  });

  it('reports a clipped box', () => {
    const { frame, body } = frameOf();
    body.style.overflow = 'hidden';
    child(body, 'wide', rect(120, 200, 2100, 300));
    expect(overflowOf(frame)).toEqual(['wide at x 120–2100, y 200–300']);
  });

  it('fails closed when the layout was not drawn', () => {
    const frame = boxed(document.createElement('div'), rect(0, 0, 1920, 1080));
    document.body.append(frame);
    expect(overflowOf(frame)).toHaveLength(1);
  });

  it('fails closed when the frame has no size', () => {
    const { frame, body } = frameOf(0);
    child(body, 'chart', rect(0, 0, 0, 0));
    expect(overflowOf(frame)).toEqual(['The layout was not measured: its frame has no size']);
  });

  it('ignores the footer', () => {
    const { frame, body, footer } = frameOf();
    boxed(footer, rect(0, 1030, 1920, 1080));
    child(body, 'chart', rect(120, 150, 1800, 996));
    expect(overflowOf(frame)).toEqual([]);
  });

  it("scales by the frame's width", () => {
    const { frame, body } = frameOf(960);
    child(body, 'inside', rect(60, 75, 900, 498));
    child(body, 'outside', rect(60, 75, 925, 498));
    expect(overflowOf(frame)).toEqual(['outside at x 120–1850, y 150–996']);
  });

  it('reports a text run that leaves the area, by its text', () => {
    const { frame, body } = frameOf();
    const line = child(body, 'headline', rect(120, 150, 1800, 300));
    line.textContent = 'A headline that runs on';
    Range.prototype.getClientRects = () => [rect(120, 150, 1900, 200)] as unknown as DOMRectList;
    expect(overflowOf(frame)).toEqual(['A headline that runs on at x 120–1900, y 150–200']);
  });

  it('lists at most 20 findings', () => {
    const { frame, body } = frameOf();
    for (let at = 0; at < 30; at += 1) child(body, `box${at}`, rect(0, 200, 50, 300));
    expect(overflowOf(frame)).toHaveLength(20);
  });
});
