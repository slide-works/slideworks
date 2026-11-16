import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { DecksLibraryView } from '../api/types';
import { NewDeckDialog } from '../look/NewDeckDialog';
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
// filters the rows by title in the browser. "+ New deck" opens the New deck
// dialog, which goes to the new deck's page once it's created; closing the
// dialog puts focus back on "+ New deck".
export function DecksPage() {
  const navigate = useNavigate();
  const [library, setLibrary] = useState<Library>({ kind: 'loading' });
  const [search, setSearch] = useState('');
  const [newDeckOpen, setNewDeckOpen] = useState(false);
  const newDeckButton = useRef<HTMLButtonElement>(null);

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

      <DecksHeader
        deckCount={view?.decks.length ?? null}
        search={search}
        onSearch={setSearch}
        newDeck={
          <button
            type="button"
            ref={newDeckButton}
            data-testid="new-deck"
            className="btn-primary py-1.5"
            aria-haspopup="dialog"
            onClick={() => setNewDeckOpen(true)}
          >
            + New deck
          </button>
        }
      />

      <NewDeckDialog
        open={newDeckOpen}
        onClose={() => {
          setNewDeckOpen(false);
          newDeckButton.current?.focus();
        }}
        onCreated={(created) => navigate(`/decks/${created.id}`)}
      />

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
