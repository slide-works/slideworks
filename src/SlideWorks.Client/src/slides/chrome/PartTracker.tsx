import { partsOf, type PartWire } from '../../api/parts';

type PartTrackerProps = {
  parts: PartWire[];
  // The current slide's part (1-based), or null when it belongs to none.
  part: number | null;
};

function stateOf(index: number, part: number | null) {
  if (part === null) return 'next';
  if (index + 1 === part) return 'on';
  return index + 1 < part ? 'past' : 'next';
}

// The deck's parts across the top of a content slide, with the current part
// highlighted and the parts before it marked as done (D5).
export function PartTracker({ parts, part }: PartTrackerProps) {
  const all = partsOf(parts);
  // A slide with no part, or in an unnumbered one, has no tracker (D23).
  if (part === null || all[part - 1]?.mark !== null) return null;
  return (
    <ol className="sw-tracker" data-testid="part-tracker" aria-label="Parts">
      {all.map(({ short, mark }, index) => {
        // Unnumbered parts come after every numbered one and are not shown.
        if (mark !== null) return null;
        const state = stateOf(index, part);
        return (
          <li
            key={index}
            data-testid="part-tracker-part"
            data-state={state}
            aria-current={state === 'on' ? 'step' : undefined}
          >
            {short}
          </li>
        );
      })}
    </ol>
  );
}
