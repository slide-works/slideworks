import { afterEach, describe, expect, it } from 'vitest';
import { gradientTextsOf, lowContrastOf } from './contrast';

type Style = Partial<Record<'color' | 'background' | 'backgroundImage' | 'fontSize', string>>;

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

afterEach(() => document.body.replaceChildren());

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

  it('skips text on a gradient and counts it', () => {
    const frame = frameOf(
      { background: 'rgb(255, 255, 255)' },
      { color: 'rgb(250, 250, 250)', fontSize: '24px' },
      'On a gradient',
      { backgroundImage: 'linear-gradient(rgb(0, 0, 0), rgb(255, 255, 255))' },
    );
    expect(lowContrastOf(frame)).toEqual([]);
    expect(gradientTextsOf(frame)).toBe(1);
  });
});
