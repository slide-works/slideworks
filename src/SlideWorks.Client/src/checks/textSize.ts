import { textRunsOf } from './canvas';
import { severityOf, type MeasureRule, type SlideFinding } from './types';

const MIN_PX = 20;
const WHERE_LENGTH = 40;
const QUOTE_LENGTH = 30;

// Text drawn under the minimum size. The slide frame is scaled by a transform, so the computed size is already in canvas pixels.
// Elements marked data-check-size="designed" are small on purpose and skipped; chrome is skipped by textRunsOf.
export const textSize: MeasureRule = (frame, slide) => {
  const found: SlideFinding[] = [];
  const seen = new Set<HTMLElement>();
  for (const run of textRunsOf(frame)) {
    // Text with no box is not drawn (an SVG <title> or <desc>).
    if (
      run.rects.length === 0 ||
      seen.has(run.element) ||
      run.element.closest('[data-check-size="designed"]')
    )
      continue;
    seen.add(run.element);
    const size = parseFloat(getComputedStyle(run.element).fontSize);
    if (!(size < MIN_PX)) continue;
    const quoted =
      run.text.length > QUOTE_LENGTH ? `${run.text.slice(0, QUOTE_LENGTH)}…` : run.text;
    found.push({
      slide,
      rule: 'smallText',
      severity: severityOf('smallText'),
      source: 'measured',
      where: `text "${run.text.slice(0, WHERE_LENGTH)}"`,
      message: `Text "${quoted}" is drawn at ${Math.round(size)} px; at least ${MIN_PX} px reads from the back of a room. Use fewer words so it can be larger.`,
    });
  }
  return found;
};
