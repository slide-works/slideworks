import { test, expect } from '@playwright/experimental-ct-react';
import { useLayoutEffect, useRef, type CSSProperties, type ReactNode } from 'react';
import { canvasOf, footerTop, textRunsOf } from './canvas';

// Layout needs a real browser, so these are component tests, not jsdom ones.
// Each mounts a frame, runs the helpers in the page and reports plain data.
type Report = {
  scale: number;
  box: { left: number; right: number };
  runs: { text: string; rects: number }[];
  footerTop: number;
};

function Frame({
  width,
  children,
  onReport,
}: {
  width: number;
  children: ReactNode;
  onReport: (report: Report) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const frame = ref.current!;
    const canvas = canvasOf(frame);
    const mark = frame.querySelector('[data-mark]');
    const box = canvas.toCanvas(mark!.getBoundingClientRect());
    onReport({
      scale: canvas.scale,
      box: { left: box.left, right: box.right },
      runs: textRunsOf(frame).map((run) => ({ text: run.text, rects: run.rects.length })),
      footerTop: footerTop(frame),
    });
  }, [onReport]);
  const style: CSSProperties = { position: 'relative', width, height: (width * 9) / 16 };
  return (
    <div ref={ref} data-testid="slide-frame" style={style}>
      {children}
    </div>
  );
}

const mark = (
  <div data-mark style={{ position: 'absolute', left: 96, top: 0, width: 192, height: 10 }} />
);

test("scales boxes by the frame's width", async ({ mount }) => {
  let report: Report | undefined;

  await mount(
    <Frame width={960} onReport={(r) => (report = r)}>
      {mark}
    </Frame>,
  );

  await expect.poll(() => report?.scale).toBe(0.5);
  expect(report!.box).toEqual({ left: 192, right: 576 });
});

test('leaves the footer, tracker and progress bar out', async ({ mount }) => {
  let report: Report | undefined;

  await mount(
    <Frame width={960} onReport={(r) => (report = r)}>
      {mark}
      <p>Body text</p>
      <div data-testid="part-tracker">Part one</div>
      <div data-testid="slide-footer">Footer text</div>
      <div data-testid="slide-progress">3 of 9</div>
    </Frame>,
  );

  await expect.poll(() => report?.runs.map((r) => r.text)).toEqual(['Body text']);
});

test('returns one rect per line of a wrapped text', async ({ mount }) => {
  let report: Report | undefined;

  await mount(
    <Frame width={960} onReport={(r) => (report = r)}>
      {mark}
      <p style={{ position: 'absolute', top: 100, width: 80, margin: 0, fontSize: 20 }}>
        one two three four five six seven
      </p>
    </Frame>,
  );

  await expect.poll(() => report?.runs.length).toBe(1);
  expect(report!.runs[0].rects).toBeGreaterThan(1);
});

test('uses 1080 when there is no footer', async ({ mount }) => {
  let report: Report | undefined;

  await mount(
    <Frame width={960} onReport={(r) => (report = r)}>
      {mark}
    </Frame>,
  );

  await expect.poll(() => report?.footerTop).toBe(1080);
});
