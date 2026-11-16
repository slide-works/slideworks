import { useLayoutEffect, useState, type RefObject } from 'react';

export const CANVAS_WIDTH = 1920;
export const CANVAS_HEIGHT = 1080;

export type CanvasFit = { scale: number; left: number; top: number };

function fit(width: number, height: number): CanvasFit {
  const scale = Math.min(width / CANVAS_WIDTH, height / CANVAS_HEIGHT);
  return {
    scale,
    left: (width - CANVAS_WIDTH * scale) / 2,
    top: (height - CANVAS_HEIGHT * scale) / 2,
  };
}

// How to fit the 1920×1080 canvas into the stage: one uniform scale from the
// top-left corner, centred, so the slide never reflows. It recomputes when the
// stage resizes, which covers window resizes and orientation changes.
export function useCanvasScale(stage: RefObject<HTMLElement | null>): CanvasFit | null {
  const [canvasFit, setCanvasFit] = useState<CanvasFit | null>(null);

  useLayoutEffect(() => {
    const element = stage.current;
    if (!element) return;
    const measure = () => setCanvasFit(fit(element.clientWidth, element.clientHeight));
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    window.addEventListener('orientationchange', measure);
    return () => {
      observer.disconnect();
      window.removeEventListener('orientationchange', measure);
    };
  }, [stage]);

  return canvasFit;
}
