import { bodyOf } from './textRuns';

// A render that cannot be measured must never read as a pass: a frame that is missing, one with no
// custom-layout body, or one showing the renderer's error state ("could not be drawn" or "not in the
// template") is a finding of its own.
const ERROR_STATES = '[data-testid="custom-layout-error"], [data-testid="unknown-layout"]';

// Why `frame` (the slide frame of the render called `what`) was not drawn, or null when it was.
export function undrawnOf(frame: HTMLElement | null, what: string): string | null {
  if (frame === null) return `The ${what} render was not found, so it was not measured`;
  if (frame.querySelector(ERROR_STATES) !== null) {
    return `The ${what} render shows the layout's error state, so it was not measured`;
  }
  if (bodyOf(frame) === null) {
    return `The ${what} render holds no custom layout, so it was not measured`;
  }
  return null;
}
