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

// A short label for a value over a bar: 11, 2.5K, 1.2M, 0.25.
export function formatValue(value: number): string {
  return COMPACT.format(clean(value));
}

const UNITS = [
  { scale: 1e9, suffix: 'B' },
  { scale: 1e6, suffix: 'M' },
  { scale: 1e3, suffix: 'K' },
] as const;

// A tick label whose precision comes from the axis step, so ticks a step apart
// never read the same: 101.4K, 101.6K, 101.8K rather than 101K, 102K, 102K.
// `largest` is the largest tick's magnitude: every tick on an axis shares its
// compact unit (K, M or B), unless the step is under a hundredth of that unit,
// when the labels stay in plain numbers.
export function formatTick(value: number, step: number, largest: number): string {
  const unit = UNITS.find((u) => largest >= u.scale && step >= u.scale / 100) ?? {
    scale: 1,
    suffix: '',
  };
  const scaled = clean(value / unit.scale) + 0; // + 0 turns -0 into 0
  if (scaled === 0) return '0';
  const digits = Math.max(0, Math.ceil(-Math.log10(clean(step / unit.scale)) - 1e-9));
  const number = new Intl.NumberFormat('en', { maximumFractionDigits: digits }).format(scaled);
  return number + unit.suffix;
}

// The labels for an axis's ticks: one per value, no two the same.
export function tickLabels(ticks: Ticks): string[] {
  const largest = Math.max(...ticks.values.map(Math.abs));
  return ticks.values.map((value) => formatTick(value, ticks.step, largest));
}
