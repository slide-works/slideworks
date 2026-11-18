import type { FlowSlots } from '../slots';

type Described = Pick<FlowSlots, 'nodes'>;

// The flow in one sentence for a screen reader: each node in order with its
// label, text and the items of a box of tiles.
export function describeFlow({ nodes }: Described): string {
  const parts = nodes.map((n) => {
    const label = n.label === undefined ? '' : ` (${n.label})`;
    const text = n.text === undefined ? '' : `, ${n.text}`;
    const items = n.items === undefined ? '' : `, holding ${n.items.join(', ')}`;
    return `${n.title}${label}${text}${items}`;
  });
  return `Flow of ${nodes.length} steps, left to right: ${parts.join('; then ')}.`;
}
