import type { StackSlots } from '../slots';

type Described = Pick<StackSlots, 'segments' | 'unit' | 'total'>;

const NUMBER = new Intl.NumberFormat('en', { maximumFractionDigits: 20 });

// The stack in one sentence for a screen reader: its segments top to bottom,
// each with its value and unit, tag and note, then the total.
export function describeStack({ segments, unit, total }: Described): string {
  const parts = segments.map((s) => {
    const value =
      s.value === undefined
        ? ''
        : ` ${NUMBER.format(s.value)}${unit === undefined ? '' : ` ${unit}`}`;
    const tag = s.tag === undefined ? '' : ` (${s.tag})`;
    const note = s.note === undefined ? '' : `, ${s.note}`;
    return `${s.label}${value}${tag}${note}`;
  });
  const end = total === undefined ? '' : `, making ${total}`;
  return `Stack of ${segments.length} segments, top to bottom: ${parts.join('; ')}${end}.`;
}
