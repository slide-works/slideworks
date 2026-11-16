import { useEffect, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { DecksLibraryView } from '../api/types';
import { ConnectBanner } from './decks/ConnectBanner';
import { DecksEmpty } from './decks/DecksEmpty';
import { DecksHeader } from './decks/DecksHeader';
import { DecksTable } from './decks/DecksTable';
import { SharedDecksTable } from './decks/SharedDecksTable';

type Library =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: DecksLibraryView }
  | { kind: 'failed'; message: string };

// /decks: the decks library, from one GET /api/decks (DecksLibraryView). Search
// filters the rows by title in the browser.
export function DecksPage() {
  const [library, setLibrary] = useState<Library>({ kind: 'loading' });
  const [search, setSearch] = useState('');

  useEffect(() => {
    let active = true;
    api.get<DecksLibraryView>('/api/decks').then(
      (view) => active && setLibrary({ kind: 'loaded', view }),
      (error: unknown) =>
        active &&
        setLibrary({
          kind: 'failed',
          message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
        }),
    );
    return () => {
      active = false;
    };
  }, []);

  const view = library.kind === 'loaded' ? library.view : null;
  const matches = <T extends { title: string }>(rows: T[]) =>
    rows.filter((row) => row.title.toLowerCase().includes(search.trim().toLowerCase()));

  return (
    <section data-testid="decks-page" className="grid gap-5">
      {view?.connect.show && <ConnectBanner mcpUrl={view.connect.mcpUrl} />}

      <DecksHeader deckCount={view?.decks.length ?? null} search={search} onSearch={setSearch} />

      {library.kind === 'failed' && (
        <p role="alert" data-testid="decks-error" className="panel px-4 py-3 text-meta text-bad">
          {library.message}
        </p>
      )}

      {view &&
        (view.decks.length === 0 ? <DecksEmpty /> : <DecksTable rows={matches(view.decks)} />)}

      {view && view.sharedWithMe.length > 0 && (
        <SharedDecksTable rows={matches(view.sharedWithMe)} />
      )}
    </section>
  );
}
