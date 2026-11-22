import { afterEach, describe, expect, it, vi } from 'vitest';
import { gradientTextsOf, lowContrastOf } from './contrast';

type Style = Partial<
  Record<'color' | 'background' | 'backgroundImage' | 'fontSize' | 'opacity', string>
>;

// A frame holding the layout's wrapper and one text element, styled inline.
function frameOf(frameStyle: Style, textStyle: Style, text = 'A line of text', parent: Style = {}) {
  const frame = document.createElement('div');
  Object.assign(frame.style, frameStyle);
  const body = document.createElement('div');
  body.setAttribute('data-custom-layout-body', '');
  const holder = document.createElement('div');
  Object.assign(holder.style, parent);
  const line = document.createElement('p');
  Object.assign(line.style, textStyle);
  line.textContent = text;
  holder.append(line);
  body.append(holder);
  frame.append(body);
  document.body.append(frame);
  return frame;
}

afterEach(() => {
  vi.restoreAllMocks();
  document.body.replaceChildren();
});

// Stubs the computed colours as Chromium serialises them (jsdom keeps only what it can parse).
function computed(colours: { frame: string; text: string }) {
  const real = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((element: Element) => {
    const style = real(element);
    // The frame (a child of body) has the background; the wrapper, holder and text are transparent.
    const background = element.parentElement === document.body ? colours.frame : 'rgba(0, 0, 0, 0)';
    return new Proxy(style, {
      get: (target, key) =>
        key === 'color'
          ? colours.text
          : key === 'backgroundColor'
            ? background
            : (Reflect.get(target, key) as unknown),
    });
  });
}

describe('lowContrastOf', () => {
  it('reports small text under 4.5', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(119, 119, 119)', fontSize: '24px' },
      'Quiet note',
    );
    expect(lowContrastOf(frame)).toEqual(['Quiet note 4.4:1, needs 4.5']);
  });

  it('passes large text at 3.0', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(119, 119, 119)', fontSize: '28px' },
    );
    expect(lowContrastOf(frame)).toEqual([]);
  });

  it('reports large text under 3.0', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(170, 170, 170)', fontSize: '40px' },
      'Faint headline',
    );
    expect(lowContrastOf(frame)).toEqual(['Faint headline 2.3:1, needs 3.0']);
  });

  it("uses the frame's background when no ancestor has one", () => {
    const frame = frameOf(
      { background: 'rgb(0, 0, 0)' },
      { color: 'rgb(40, 40, 40)', fontSize: '24px' },
    );
    expect(lowContrastOf(frame)).toHaveLength(1);
  });

  it('uses the nearest ancestor with a solid background', () => {
    const frame = frameOf(
      { background: 'rgb(0, 0, 0)' },
      { color: 'rgb(255, 255, 255)', fontSize: '24px' },
      'On white',
      { background: 'rgb(255, 255, 255)' },
    );
    expect(lowContrastOf(frame)).toHaveLength(1);
  });

  it('fails closed on text on a gradient, and counts it', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(250, 250, 250)', fontSize: '24px' },
      'On a gradient',
      { backgroundImage: 'linear-gradient(rgb(0, 0, 0), rgb(255, 255, 255))' },
    );
    expect(lowContrastOf(frame)).toEqual([
      'On a gradient sits on a gradient or image, so its contrast was not checked',
    ]);
    expect(gradientTextsOf(frame)).toBe(1);
  });

  it('blends transparent text over the background', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgba(0, 0, 0, 0.05)', fontSize: '24px' },
      'Ghost text',
    );
    expect(lowContrastOf(frame)).toEqual(['Ghost text 1.1:1, needs 4.5']);
  });

  it('passes text that is half transparent but still dark enough', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgba(0, 0, 0, 0.8)', fontSize: '24px' },
    );
    expect(lowContrastOf(frame)).toEqual([]);
  });

  it('composites a transparent background over the one behind it', () => {
    // White at 5% over black is near black: white text on it passes, black text does not.
    const light = frameOf(
      { background: 'rgb(0, 0, 0)' },
      { color: 'rgb(255, 255, 255)', fontSize: '24px' },
      'Light on dark',
      { background: 'rgba(255, 255, 255, 0.05)' },
    );
    expect(lowContrastOf(light)).toEqual([]);
    document.body.replaceChildren();
    const dark = frameOf(
      { background: 'rgb(0, 0, 0)' },
      { color: 'rgb(0, 0, 0)', fontSize: '24px' },
      'Dark on dark',
      { background: 'rgba(255, 255, 255, 0.05)' },
    );
    expect(lowContrastOf(dark)).toHaveLength(1);
  });

  it('fails closed when the backdrop is see-through all the way down', () => {
    const frame = frameOf(
      { background: 'rgba(255, 255, 255, 0.5)' },
      { color: 'rgb(0, 0, 0)', fontSize: '24px' },
      'Floating',
    );
    expect(lowContrastOf(frame)).toEqual([
      'Floating sits on a see-through background, so its contrast was not checked',
    ]);
  });

  it('fails closed under an ancestor with opacity', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(0, 0, 0)', fontSize: '24px' },
      'Faded out',
      { opacity: '0.05' },
    );
    expect(lowContrastOf(frame)).toEqual([
      "Faded out sits under opacity, so its contrast can't be measured",
    ]);
  });

  it('fails closed on nested backgrounds under an opacity ancestor', () => {
    // Opacity composites the group after its children: no per-layer ratio is trusted.
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(60, 60, 60)', fontSize: '24px' },
      'Nested',
      { opacity: '0.5', background: 'rgb(0, 0, 0)' },
    );
    const inner = document.createElement('section');
    inner.style.background = 'rgb(255, 255, 255)';
    const holder = frame.querySelector('[data-custom-layout-body] > div') as HTMLElement;
    const line = holder.querySelector('p') as HTMLElement;
    inner.append(line);
    holder.append(inner);
    expect(lowContrastOf(frame)).toEqual([
      "Nested sits under opacity, so its contrast can't be measured",
    ]);
  });

  it('fails closed when the layout was not drawn', () => {
    const frame = document.createElement('div');
    document.body.append(frame);
    expect(lowContrastOf(frame)).toHaveLength(1);
  });
});

describe('color(srgb …) computed colours', () => {
  const plain = { fontSize: '24px' };

  it('reads a color-mix background and text colour', () => {
    const frame = frameOf({}, plain, 'On a chip');
    computed({ frame: 'color(srgb 0.1 0.1 0.12)', text: 'color(srgb 0.95 0.95 0.95 / 1)' });
    expect(lowContrastOf(frame)).toEqual([]);
  });

  it('measures low contrast on a color-mix background', () => {
    const frame = frameOf({}, plain, 'Dim chip');
    computed({ frame: 'color(srgb 0.1 0.1 0.12)', text: 'color(srgb 0.2 0.2 0.22)' });
    expect(lowContrastOf(frame)).toHaveLength(1);
    expect(lowContrastOf(frame)[0]).toMatch(/^Dim chip \d\.\d:1, needs 4\.5$/);
  });

  it('takes none as 0, percentages, and alpha', () => {
    const frame = frameOf({}, plain, 'Pct');
    computed({ frame: 'color(srgb none 0% 0%)', text: 'color(srgb 100% 100% 100% / 50%)' });
    expect(lowContrastOf(frame)).toEqual([]);
  });

  it.each(['color(oklch 0.5 0.1 200)', 'color(display-p3 1 0 0)', 'oklch(50% 0.1 200)'])(
    'fails closed on %s',
    (other) => {
      const frame = frameOf({}, plain, 'Other space');
      computed({ frame: other, text: 'color(srgb 1 1 1)' });
      expect(lowContrastOf(frame)).toEqual([
        'Other space has a colour that could not be read, so its contrast was not checked',
      ]);
    },
  );
});
