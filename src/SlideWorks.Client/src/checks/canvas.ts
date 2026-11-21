// The 1920x1080 canvas maths shared by the measured rules. A slide is drawn at
// 1920x1080 and scaled to fit its frame, so every box is divided by the scale
// to give canvas pixels. Colours are never written here; rules read them from
// getComputedStyle.

export const CANVAS_WIDTH = 1920;
export const CANVAS_HEIGHT = 1080;

export type CanvasRect = { left: number; top: number; right: number; bottom: number };

export type Canvas = {
  scale: number;
  toCanvas: (rect: DOMRect) => CanvasRect;
};

export type TextRun = {
  node: Text;
  element: HTMLElement;
  text: string;
  rects: CanvasRect[];
};

const CHROME = ['part-tracker', 'slide-footer', 'slide-progress'];

// True for the frame's own part tracker, footer and progress bar.
export function isChrome(el: Element): boolean {
  return CHROME.some((id) => el.closest(`[data-testid="${id}"]`) !== null);
}

export function canvasOf(frame: HTMLElement): Canvas {
  const box = frame.getBoundingClientRect();
  const scale = box.width / CANVAS_WIDTH;
  return {
    scale,
    toCanvas: (rect) => ({
      left: (rect.left - box.left) / scale,
      top: (rect.top - box.top) / scale,
      right: (rect.right - box.left) / scale,
      bottom: (rect.bottom - box.top) / scale,
    }),
  };
}

// Every non-blank text node outside the chrome, with one rect per line it draws.
export function textRunsOf(frame: HTMLElement): TextRun[] {
  const canvas = canvasOf(frame);
  const runs: TextRun[] = [];
  const walker = document.createTreeWalker(frame, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const element = node.parentElement;
    const text = (node.textContent ?? '').trim();
    if (!element || !text || isChrome(element)) continue;
    const range = document.createRange();
    range.selectNodeContents(node);
    const rects = Array.from(range.getClientRects())
      .filter((rect) => rect.width !== 0 || rect.height !== 0)
      .map((rect) => canvas.toCanvas(rect));
    runs.push({ node: node as Text, element, text, rects });
  }
  return runs;
}

// The footer's top in canvas pixels, or the canvas height when there is none.
export function footerTop(frame: HTMLElement): number {
  const footer = frame.querySelector('[data-testid="slide-footer"]');
  if (!footer) return CANVAS_HEIGHT;
  return canvasOf(frame).toCanvas(footer.getBoundingClientRect()).top;
}
