import { afterEach, describe, expect, it } from 'vitest';
import { undrawnOf } from './undrawn';

type Child = { tag: string; attribute?: [string, string] };

const frameWith = (...children: Child[]) => {
  const frame = document.createElement('div');
  for (const { tag, attribute } of children) {
    const element = document.createElement(tag);
    if (attribute) element.setAttribute(...attribute);
    frame.append(element);
  }
  document.body.append(frame);
  return frame;
};

afterEach(() => document.body.replaceChildren());

describe('undrawnOf', () => {
  it('flags a missing frame', () => {
    expect(undrawnOf(null, 'maximum')).toMatch(/maximum render was not found/);
  });

  it('flags a frame with no custom layout', () => {
    expect(undrawnOf(frameWith({ tag: 'p' }), 'sample')).toMatch(/holds no custom layout/);
  });

  it.each(['custom-layout-error', 'unknown-layout'])('flags the %s state', (testId) => {
    const frame = frameWith({ tag: 'p', attribute: ['data-testid', testId] });
    expect(undrawnOf(frame, 'dark')).toMatch(/error state/);
  });

  it('passes a drawn layout', () => {
    const frame = frameWith({ tag: 'div', attribute: ['data-custom-layout-body', ''] });
    expect(undrawnOf(frame, 'sample')).toBeNull();
  });
});
