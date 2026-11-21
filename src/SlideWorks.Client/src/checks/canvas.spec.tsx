import { test, expect } from '@playwright/experimental-ct-react';
import { CanvasProbe, type CanvasReport } from '../test-support/CanvasProbe';

// Layout needs a real browser, so these are component tests, not jsdom ones.
const mark = (
  <div data-mark style={{ position: 'absolute', left: 96, top: 0, width: 192, height: 10 }} />
);

test("scales boxes by the frame's width", async ({ mount }) => {
  let report: CanvasReport | undefined;

  await mount(
    <CanvasProbe width={960} onReport={(r) => (report = r)}>
      {mark}
    </CanvasProbe>,
  );

  await expect.poll(() => report?.scale).toBe(0.5);
  expect(report!.box).toEqual({ left: 192, right: 576 });
});

test('leaves the footer, tracker and progress bar out', async ({ mount }) => {
  let report: CanvasReport | undefined;

  await mount(
    <CanvasProbe width={960} onReport={(r) => (report = r)}>
      {mark}
      <p>Body text</p>
      <div data-testid="part-tracker">Part one</div>
      <div data-testid="slide-footer">Footer text</div>
      <div data-testid="slide-progress">3 of 9</div>
    </CanvasProbe>,
  );

  await expect.poll(() => report?.runs.map((r) => r.text)).toEqual(['Body text']);
});

test('returns one rect per line of a wrapped text', async ({ mount }) => {
  let report: CanvasReport | undefined;

  await mount(
    <CanvasProbe width={960} onReport={(r) => (report = r)}>
      {mark}
      <p style={{ position: 'absolute', top: 100, width: 80, margin: 0, fontSize: 20 }}>
        one two three four five six seven
      </p>
    </CanvasProbe>,
  );

  await expect.poll(() => report?.runs.length).toBe(1);
  expect(report!.runs[0].rects).toBeGreaterThan(1);
});

test('uses 1080 when there is no footer', async ({ mount }) => {
  let report: CanvasReport | undefined;

  await mount(
    <CanvasProbe width={960} onReport={(r) => (report = r)}>
      {mark}
    </CanvasProbe>,
  );

  await expect.poll(() => report?.footerTop).toBe(1080);
});

test('gives a footer at 486 px in a 960 px frame a top of 972', async ({ mount }) => {
  let report: CanvasReport | undefined;

  await mount(
    <CanvasProbe width={960} onReport={(r) => (report = r)}>
      {mark}
      <div
        data-testid="slide-footer"
        style={{ position: 'absolute', left: 0, top: 486, width: 960, height: 54 }}
      />
    </CanvasProbe>,
  );

  await expect.poll(() => report?.footerTop).toBe(972);
});
