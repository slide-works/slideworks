// What safeArea.ts and contrast.ts both walk: the wrapper that holds a custom layout's own elements
// (the slide frame's own chrome is outside it) and the text nodes in it.

export const CANVAS_WIDTH = 1920;

export const BODY_SELECTOR = '[data-custom-layout-body]';

// The attribute the custom renderer puts on every built-in block and image it draws.
export const BLOCK_ATTRIBUTE = 'data-sw-block';

// True when `element` is a strict descendant of a built-in block.
export const insideBlock = (element: Element): boolean =>
  element.parentElement?.closest(`[${BLOCK_ATTRIBUTE}]`) != null;

// The wrapper under a slide frame, or null when the frame holds no custom layout.
export const bodyOf = (frame: HTMLElement): HTMLElement | null =>
  frame.querySelector<HTMLElement>(BODY_SELECTOR);

// How many screen pixels one canvas pixel takes: the frame is 1920 wide on the canvas.
export const scaleOf = (frame: HTMLElement): number => {
  const width = frame.getBoundingClientRect().width;
  return width > 0 ? width / CANVAS_WIDTH : 1;
};

// Every text node under `root` that holds more than white space.
export function textNodesOf(root: HTMLElement): Text[] {
  const found: Text[] = [];
  const walker = root.ownerDocument.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
    if ((node.textContent ?? '').trim() !== '') found.push(node as Text);
  }
  return found;
}

// The text as a person reads it in a finding: trimmed, white space collapsed, cut at 30 characters.
export const textStart = (text: string): string => {
  const flat = text.replace(/\s+/g, ' ').trim();
  return flat.length > 30 ? `${flat.slice(0, 30)}…` : flat;
};
