// Scales and axis helpers for the hand-drawn chart (D18): nice tick values, a
// linear scale and compact tick labels. No chart library.

export type Ticks = { lo: number; hi: number; step: number; values: number[] };

// Rounds away the float noise that repeated steps leave, such as 0.30000000000000004.
function clean(value: number): number {
  return Number(value.toPrecision(12));
}

// The 1, 2, 5 or 10 × a power of ten nearest to `raw` (Heckbert's nice numbers).
export function niceStep(raw: number): number {
  const magnitude = 10 ** Math.floor(Math.log10(raw));
  const fraction = clean(raw / magnitude);
  const nice = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
  return clean(nice * magnitude);
}

// Evenly spaced round tick values from at or below `min` to at or above `max`,
// about `target` steps apart. Equal ends are widened so the axis has a range.
export function niceTicks(min: number, max: number, target = 5): Ticks {
  if (!Number.isFinite(min) || !Number.isFinite(max) || min > max) {
    throw new RangeError(`niceTicks needs finite min <= max (got ${min}, ${max})`);
  }
  let low = min;
  let high = max;
  if (low === high) {
    const pad = low === 0 ? 1 : Math.abs(low) / 2;
    low = low >= 0 && low - pad < 0 ? 0 : low - pad;
    high += pad;
  }
  const step = niceStep((high - low) / target);
  const lo = clean(Math.floor(low / step) * step);
  const hi = clean(Math.ceil(high / step) * step);
  const count = Math.round((hi - lo) / step);
  const values = Array.from({ length: count + 1 }, (_, i) => clean(lo + i * step));
  return { lo, hi, step, values };
}

// Maps [d0, d1] onto [r0, r1].
export function linear(d0: number, d1: number, r0: number, r1: number): (value: number) => number {
  const span = d1 - d0;
  return (value) => (span === 0 ? r0 : r0 + ((value - d0) / span) * (r1 - r0));
}

const COMPACT = new Intl.NumberFormat('en', {
  notation: 'compact',
  maximumSignificantDigits: 3,
});

// A short label for a tick or value: 11, 2.5K, 1.2M, 0.25.
export function formatValue(value: number): string {
  return COMPACT.format(clean(value));
}
