import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { canvasOf } from './canvas';
import { images } from './images';

// jsdom has no layout and never loads an image, so the natural size and the drawn box are stubbed.
function frameWith(drawnWidth: number, natural: { w: number; h: number; complete?: boolean }) {
  const frame = document.createElement('div');
  const img = document.createElement('img');
  frame.append(img);
  Object.defineProperty(img, 'naturalWidth', { value: natural.w });
  Object.defineProperty(img, 'naturalHeight', { value: natural.h });
  Object.defineProperty(img, 'complete', { value: natural.complete ?? true });
  const rects = new Map<Element, DOMRect>([
    [frame, { left: 0, top: 0, right: 1920, bottom: 1080, width: 1920, height: 1080 } as DOMRect],
    [
      img,
      {
        left: 0,
        top: 0,
        right: drawnWidth,
        bottom: 100,
        width: drawnWidth,
        height: 100,
      } as DOMRect,
    ],
  ]);
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    return rects.get(this)!;
  });
  return frame;
}

beforeEach(() => vi.restoreAllMocks());
afterEach(() => vi.restoreAllMocks());

const run = (frame: HTMLElement) => images(frame, 2, canvasOf(frame));

test('reports an image that did not load', () => {
  const found = run(frameWith(400, { w: 0, h: 0, complete: true }));

  expect(found).toEqual([
    {
      slide: 2,
      rule: 'imageNotLoaded',
      severity: 'error',
      source: 'measured',
      where: 'img',
      message: "An image didn't load. Upload it again with upload_image.",
    },
  ]);
});

test('reports an image drawn twice its width', () => {
  const found = run(frameWith(1200, { w: 600, h: 4000 }));

  expect(found).toEqual([
    {
      slide: 2,
      rule: 'imageLowResolution',
      severity: 'warning',
      source: 'measured',
      where: 'img',
      message:
        'This image is 600 px wide but drawn 1200 px wide, so it will look blurry. Upload one at least 1200 px wide.',
    },
  ]);
});

test('accepts an image drawn at 1.5x', () => {
  expect(run(frameWith(900, { w: 600, h: 4000 }))).toEqual([]);
});
