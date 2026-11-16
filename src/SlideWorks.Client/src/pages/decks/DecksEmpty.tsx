// The library before the presenter has any deck of their own.
export function DecksEmpty() {
  return (
    <div data-testid="decks-empty" className="panel grid gap-1 px-6 py-10 text-center">
      <p className="font-semibold text-ink">No decks yet</p>
      <p className="text-meta text-ink-muted">
        Ask a connected agent to make one, or start one with + New deck.
      </p>
    </div>
  );
}
