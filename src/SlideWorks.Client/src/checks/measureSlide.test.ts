import { expect, test, vi } from 'vitest';
import { measureSlide } from './measureSlide';

const { calls, standIn } = vi.hoisted(() => {
  const calls: string[] = [];
  const standIn = (name: string, rule: string) => () => {
    calls.push(name);
    return [
      { slide: 4, rule, severity: 'warning', source: 'measured', where: rule, message: rule },
    ];
  };
  return { calls, standIn };
});

vi.mock('./overflow', () => ({ overflow: standIn('overflow', 'overflow') }));
vi.mock('./images', () => ({ images: standIn('images', 'imageNotLoaded') }));
vi.mock('./contrast', () => ({ contrast: standIn('contrast', 'lowContrast') }));
vi.mock('./textSize', () => ({ textSize: standIn('textSize', 'smallText') }));

test('runs the four rules in order and concatenates their findings', () => {
  const frame = document.createElement('div');
  frame.getBoundingClientRect = () => ({ width: 960, height: 540 }) as DOMRect;

  const found = measureSlide(frame, 4);

  expect(calls).toEqual(['overflow', 'images', 'contrast', 'textSize']);
  expect(found.map((f) => f.rule)).toEqual([
    'overflow',
    'imageNotLoaded',
    'lowContrast',
    'smallText',
  ]);
  expect(found.map((f) => f.language)).toEqual([null, null, null, null]);
});

test('measures nothing while the frame has no width', () => {
  calls.length = 0;
  const frame = document.createElement('div');
  frame.getBoundingClientRect = () => ({ width: 0, height: 0 }) as DOMRect;

  expect(measureSlide(frame, 4)).toEqual([]);
  expect(calls).toEqual([]);
});
