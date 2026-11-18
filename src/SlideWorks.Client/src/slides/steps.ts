import type { CalloutBlock } from './slots';

// The step a body callout appears at (K7): 0, shown from the start, when there
// is no callout or it does not reveal last; otherwise the step after every
// other step of the slide. The slide's step count comes from the server; a
// layout passes the number of steps its own content takes.
export function calloutStep(otherSteps: number, callout?: CalloutBlock): number {
  return callout?.reveal === 'last' ? otherSteps + 1 : 0;
}
