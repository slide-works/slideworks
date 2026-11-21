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
const greyAlpha = (level: number, alpha: number) => `rgba(${level}, ${level}, ${level}, ${alpha})`;
// Chromium's computed form of a color-mix(): channels 0-1.
const srgbGrey = (level: number, alpha = 1) =>
  `color(srgb ${level / 255} ${level / 255} ${level / 255} / ${alpha})`;

// jsdom's CSS parser drops color(), so a style that carries one is read through a stub of getComputedStyle.
function computedWith(overrides: Map<Element, Record<string, string>>) {
  const real = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((el: Element, pseudo?: string | null) => {
    const style = real(el, pseudo);
    const extra = overrides.get(el);
    if (!extra) return style;
    return new Proxy(style, {
      get: (target, key) => {
        if (key === 'getPropertyValue')
          return (name: string) => extra[name] ?? target.getPropertyValue(name);
        return typeof key === 'string' && key in extra ? extra[key] : Reflect.get(target, key);
      },
    });
  });
}

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
  band.style.backgroundImage = `linear-gradient(to right, ${grey(0)}, ${grey(255)})`;
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

test('scores translucent text at its alpha', () => {
  // Black at 30% over white is about grey 179, 2.1:1.
  const frame = frameOf({ backgroundColor: grey(255) }, [
    textEl('Faint', { color: greyAlpha(0, 0.3), fontSize: '24px' }),
  ]);
  expect(run(frame)).toHaveLength(1);
});

test('blends a translucent panel over the solid colour below it', () => {
  // A 50% black panel over white is grey 128; grey 150 text on it fails, but would pass on plain white's neighbour grey 20.
  const panel = document.createElement('div');
  panel.style.backgroundColor = greyAlpha(0, 0.5);
  panel.append(textEl('On a panel', { color: grey(150), fontSize: '24px' }));
  const frame = frameOf({ backgroundColor: grey(255) }, [panel]);
  expect(run(frame)).toHaveLength(1);
});

test('reads a color(srgb) string', () => {
  const el = textEl('Mixed', { fontSize: '24px' });
  const frame = frameOf({ backgroundColor: grey(0) }, [el]);
  computedWith(new Map([[el, { color: srgbGrey(30) }]]));
  const found = run(frame);
  expect(found).toHaveLength(1);
  expect(found[0]!.rule).toBe('lowContrast');
});

test('reads a color(srgb) background that carries alpha', () => {
  const panel = document.createElement('div');
  const el = textEl('On mixed', { color: grey(255), fontSize: '24px' });
  panel.append(el);
  const frame = frameOf({ backgroundColor: grey(255) }, [panel]);
  computedWith(new Map([[panel, { backgroundColor: srgbGrey(240, 0.5) }]]));
  expect(run(frame)).toHaveLength(1);
});

test('holds text just under 28 px to 4.5 and text at 28 px to 3.0', () => {
  const small = textEl('Just under', { color: grey(140), fontSize: '27px' });
  const large = textEl('At the line', { color: grey(140), fontSize: '28px' });
  const found = run(frameOf({ backgroundColor: grey(255) }, [small, large]));
  expect(found).toHaveLength(1);
  expect(found[0]!.where).toContain('Just under');
});

test('measures the stroke of transparent outlined text', () => {
  const el = textEl('01', { color: greyAlpha(0, 0), fontSize: '60px' });
  const frame = frameOf({ backgroundColor: grey(255) }, [el]);
  computedWith(
    new Map([[el, { '-webkit-text-stroke-color': grey(250), '-webkit-text-stroke-width': '2px' }]]),
  );
  expect(run(frame)).toHaveLength(1);
});

test('skips transparent text with no stroke', () => {
  const frame = frameOf({ backgroundColor: grey(255) }, [
    textEl('Clipped', { color: greyAlpha(0, 0), fontSize: '60px' }),
  ]);
  expect(run(frame)).toEqual([]);
});

test('skips text with no box', () => {
  const frame = frameOf({ backgroundColor: grey(255) }, [
    textEl('Hidden', { color: grey(250), fontSize: '24px' }, [0, 0, 0, 0]),
  ]);
  expect(run(frame)).toEqual([]);
});
