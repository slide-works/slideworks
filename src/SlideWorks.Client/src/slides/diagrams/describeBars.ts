import type { BarsSlots } from '../slots';
import { formatValue } from './barsGeometry';

type Described = Pick<BarsSlots, 'bars' | 'unit' | 'values' | 'wasLabel'>;

// The bars in one sentence for a screen reader: each bar with its value and
// unit (unless hidden), what it was, its tag and its note.
export function describeBars({ bars, unit, values, wasLabel }: Described): string {
  const shown = values !== 'hidden';
  const parts = bars.map((b) => {
    const value = shown ? ` ${formatValue(b.value)}${unit === undefined ? '' : ` ${unit}`}` : '';
    const was = b.was === undefined ? '' : `, ${wasLabel ?? 'was'} ${formatValue(b.was)}`;
    const tag = b.tag === undefined ? '' : ` (${b.tag})`;
    const note = b.note === undefined ? '' : `, ${b.note}`;
    return `${b.label}${value}${was}${tag}${note}`;
  });
  return `Bars comparing ${bars.length} things: ${parts.join('; ')}.`;
}
