import type { ContrastSummary } from '../../api/types';

// The palette's contrast check in words, from the deck page's look.contrast.
export function contrastMessage({ pass, lowest }: ContrastSummary): string {
  const ratio = `${lowest.ratio.toFixed(1)}:1`;
  const bar = `${lowest.bar.toFixed(1)}:1`;
  return pass
    ? `All pairs pass. Closest: ${lowest.pair} at ${ratio} (needs ${bar}).`
    : `${lowest.pair} is ${ratio}, below the ${bar} it needs.`;
}
