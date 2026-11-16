// The deck's parts as a timeline, with `part` (1-based) as the current one.
// Rendered from the deck, never set by an agent.
// TODO: stub; the real journey block replaces it.
export function Journey({ parts, part }: { parts: string[]; part: number | null }) {
  return (
    <ol data-testid="block-journey">
      {parts.map((name, index) => (
        <li key={name} aria-current={index + 1 === part ? 'step' : undefined}>
          {name}
        </li>
      ))}
    </ol>
  );
}
