import { useLayoutEffect, useRef, type CSSProperties, type ReactNode } from 'react';
import { canvasOf, footerTop, textRunsOf } from '../checks/canvas';

// Mounts a slide frame, runs the canvas helpers in the page and reports plain data.
export type CanvasReport = {
  scale: number;
  box: { left: number; right: number };
  runs: { text: string; rects: number }[];
  footerTop: number;
};

export function CanvasProbe({
  width,
  children,
  onReport,
}: {
  width: number;
  children: ReactNode;
  onReport: (report: CanvasReport) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const frame = ref.current!;
    const canvas = canvasOf(frame);
    const box = canvas.toCanvas(frame.querySelector('[data-mark]')!.getBoundingClientRect());
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
