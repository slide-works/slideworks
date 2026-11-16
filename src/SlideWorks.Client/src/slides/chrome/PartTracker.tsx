type PartTrackerProps = {
  parts: string[];
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
  return (
    <ol className="sw-tracker" data-testid="part-tracker" aria-label="Parts">
      {parts.map((name, index) => {
        const state = stateOf(index, part);
        return (
          <li
            key={index}
            data-testid="part-tracker-part"
            data-state={state}
            aria-current={state === 'on' ? 'step' : undefined}
          >
            {name}
          </li>
        );
      })}
    </ol>
  );
}
