import type { LookView } from '../../api/types';

// The template has a light and a dark logo. Then the pair draws in every
// footer, on light and dark slides alike, and the deck's own logo does not:
// one file cannot be read on both surfaces.
export function hasTemplateLogoPair(logos: LookView['logos']): boolean {
  return logos.light !== null && logos.dark !== null;
}
