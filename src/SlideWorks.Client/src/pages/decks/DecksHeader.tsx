import { NewDeckForm } from './NewDeckForm';

type DecksHeaderProps = {
  deckCount: number | null;
  search: string;
  onSearch(search: string): void;
};

// The library's heading row: "Decks" with the count, the title search and
// "+ New deck".
export function DecksHeader({ deckCount, search, onSearch }: DecksHeaderProps) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <h2 className="text-lg font-bold tracking-tight text-ink">Decks</h2>
      {deckCount !== null && (
        <span
          data-testid="decks-count"
          className="rounded-full bg-panel-2 px-2 py-0.5 font-mono text-xs text-ink-muted"
        >
          {deckCount}
        </span>
      )}
      <input
        type="search"
        aria-label="Search decks"
        placeholder="Search decks"
        data-testid="decks-search"
        className="field ml-auto w-full py-1.5 sm:w-56"
        value={search}
        onChange={(event) => onSearch(event.target.value)}
      />
      <NewDeckForm />
    </div>
  );
}
